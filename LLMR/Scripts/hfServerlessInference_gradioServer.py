import gradio as gr
import requests
import json
import os
import uuid
import sys
import argparse
import datetime
import re  # note from Moe: leave regex for post-processing! (removing "User:"s in replies)

def run_gradio(api_token, model_id, system_message, temperature, max_completion_tokens, top_p, frequency_penalty, presence_penalty, stop_sequences):
    api_url = f"https://api-inference.huggingface.co/models/{model_id}"
    headers = {
        "Authorization": f"Bearer {api_token}",
        "Content-Type": "application/json"
    }

    def generate_unique_id():
        return uuid.uuid4().hex

    def predict(message, history, unique_id_state):
        if unique_id_state is None:
            unique_id = generate_unique_id()
            unique_id_state = unique_id  # set  unique ID
        else:
            unique_id = unique_id_state  # retrieve existing unique ID

        # build  conversation as list of mesgs
        messages = []
        #  system message
        if system_message:
            messages.append({"role": "system", "content": system_message})

        # add conversation history
        if history:
            messages.extend(history)

        # add the new user message
        messages.append({"role": "user", "content": message})

        # parse the stop sequences
        if stop_sequences:
            stop_sequences_list = [seq.encode('utf-8').decode('unicode_escape').strip() for seq in stop_sequences.split(',')]
        else:
            stop_sequences_list = ["User:"]

        # prepare parameters
        parameters = {
            "temperature": temperature,
            "max_new_tokens": max_completion_tokens or 512,
            "frequency_penalty": frequency_penalty or 0.0,
            "presence_penalty": presence_penalty or 0.0,
            "stop": stop_sequences_list,
        }

        # only include top_p if it's valid (between 0.0 and 1.0, exclusive)
        if 0.0 < top_p < 1.0:
            parameters["top_p"] = top_p

        # messages to single string for  API
        conversation = ""
        for msg in messages:
            if msg["role"] == "system":
                conversation += f"{msg['content']}\n\n"
            elif msg["role"] == "user":
                conversation += f"User: {msg['content']}\n"
            elif msg["role"] == "assistant":
                conversation += f"Assistant: {msg['content']}\n"

        conversation += "Assistant:"

        payload = {
            "inputs": conversation,
            "parameters": parameters,
            "options": {
                "wait_for_model": False,  
                "use_cache": False,
            },
        }

        try:
            response = requests.post(api_url, headers=headers, json=payload, timeout=60)
            if response.status_code == 503:
                raise Exception("<HFGS.py> 503: Model is loading. Please try again shortly.")
            elif response.status_code != 200:
                raise Exception(f"<HFGS.py> Request failed with status code {response.status_code}: {response.text}")

            output = response.json()
            # handle case where output is list
            if isinstance(output, list):
                if len(output) > 0:
                    generated_text = output[0].get("generated_text", "")
                else:
                    generated_text = ""
            elif isinstance(output, dict):
                generated_text = output.get("generated_text", "")
            else:
                generated_text = ""

            # assistant's reply
            assistant_reply = generated_text[len(conversation):].strip()

            # POST-PROCESSING: Remove any trailing 'User:' & following text
            assistant_reply = re.split(r'User:', assistant_reply, flags=re.IGNORECASE)[0].strip()

            # update history
            new_history = history or []
            new_history.append({"role": "user", "content": message})
            new_history.append({"role": "assistant", "content": assistant_reply})

            # save 
            download_history(new_history, unique_id)

            # yield updated hist
            yield new_history, unique_id_state, ""

        except requests.exceptions.Timeout:
            error_message = "An error occurred: The request timed out."
            history.append({"role": "assistant", "content": error_message})
            yield history, unique_id_state, ""
        except Exception as e:
            error_message = f"An error occurred: {str(e)}"
            history.append({"role": "assistant", "content": error_message})
            yield history, unique_id_state, ""

    def download_history(history, unique_id):
        # hist to list of user-assistant pairs
        history_json = []
        current_pair = {}
        for msg in history:
            if msg["role"] == "user":
                current_pair = {"user": msg["content"]}
            elif msg["role"] == "assistant":
                if "user" in current_pair:
                    current_pair["assistant"] = msg["content"]
                    history_json.append(current_pair)
                    current_pair = {}
        # handle any remaining msg
        if current_pair:
            history_json.append(current_pair)

        settings = {
            "api_token": api_token,
            "model_id": model_id,
            "system_message": system_message,
            "temperature": temperature,
            "max_completion_tokens": max_completion_tokens,
            "top_p": top_p,
            "frequency_penalty": frequency_penalty,
            "presence_penalty": presence_penalty,
            "downloaded_on": datetime.datetime.now().strftime("%B %d, %Y at %H:%M:%S")  
        }

        full_data = {
            "settings": settings,
            "conversation": history_json
        }

        try:
            print("<hfSI_gS.py internal> History is being downloaded...")

            script_dir = os.path.dirname(os.path.abspath(__file__))
            chat_dir = os.path.join(script_dir, "chat_histories")
            os.makedirs(chat_dir, exist_ok=True)

            filename = f"chathistory_{unique_id}.json"
            filepath = os.path.join(chat_dir, filename)

            print(f"<hfSI_gS.py internal> History is being saved as {filename} in the directory {filepath}.")

            with open(filepath, "w") as f:
                json.dump(full_data, f, indent=4)  # save settings and history

            print("<hfSI_gS.py internal> History was successfully downloaded.")

            return filepath
        except Exception as e:
            return f"Error saving chat history: {str(e)}"

    def create_interface():
        with gr.Blocks() as iface:
            chatbot = gr.Chatbot(type="messages")
            msg = gr.Textbox(label="Send message to the LLM")
            clear = gr.Button("Clear")
            unique_id_label = gr.Label(value="")  # display unique ID
            state = gr.State(value=None)  # init session state for unique ID

            def update_unique_id(state):
                if state is None:
                    state = generate_unique_id()
                return f"Your unique ID: {state}", state

            # update unique ID
            iface.load(fn=update_unique_id, inputs=state, outputs=[unique_id_label, state])

            # pass message, hist & ID state to predict
            msg.submit(predict, inputs=[msg, chatbot, state], outputs=[chatbot, state, msg])
            clear.click(lambda: ([], None, ""), None, [chatbot, state, msg], queue=False)

        return iface

    interface = create_interface()
    interface.launch(share=True)  

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Hugging Face Serverless Inference Gradio Server")
    parser.add_argument('--start-gradio', action='store_true', help='Start Gradio Interface')
    parser.add_argument('--api_token', type=str, required=True, help='Hugging Face API Token')
    parser.add_argument('--model_id', type=str, default='meta-llama/Llama-2-7b-chat-hf', help='Model ID')
    parser.add_argument('--system_message', type=str, default='You are a helpful assistant.', help='System message')
    parser.add_argument('--temperature', type=float, default=0.8, help='Temperature (0 to 2)')
    parser.add_argument('--max_completion_tokens', type=int, help='Max completion tokens (optional)')
    parser.add_argument('--top_p', type=float, default=0.95, help='Top P (0.0 < top_p < 1.0)')
    parser.add_argument('--frequency_penalty', type=float, default=0.0, help='Frequency penalty (-2.0 to 2.0)')
    parser.add_argument('--presence_penalty', type=float, default=0.0, help='Presence penalty (-2.0 to 2.0)')
    parser.add_argument('--stop_sequences', type=str, default="User:,\\nUser:", help='Comma-separated list of stop sequences')

    args = parser.parse_args()

    if args.start_gradio:
        run_gradio(
            api_token=args.api_token,
            model_id=args.model_id,
            system_message=args.system_message,
            temperature=args.temperature,
            max_completion_tokens=args.max_completion_tokens,
            top_p=args.top_p,
            frequency_penalty=args.frequency_penalty,
            presence_penalty=args.presence_penalty,
            stop_sequences=args.stop_sequences
        )
    else:
        print("<hfSI_gS.py internal> Gradio interface is not running. Use --start-gradio to start the interface.")
