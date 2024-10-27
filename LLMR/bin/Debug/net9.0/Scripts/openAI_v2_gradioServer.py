import gradio as gr
import openai
import json
import os
import uuid
import sys
import argparse
import datetime

def run_gradio(api_key, model, system_message, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
    client = openai.Client(api_key=api_key)

    def generate_unique_id():
        return uuid.uuid4().hex

    def predict(message, history, unique_id_state):
        if unique_id_state is None:
            unique_id = generate_unique_id()
            unique_id_state = unique_id  
        else:
            unique_id = unique_id_state  

        # system message
        messages = [{"role": "system", "content": system_message}]
        # previous history (user msgs, not system msg!)
        if history:
            messages.extend(history)
        # new user message
        messages.append({"role": "user", "content": message})

        try:
            partial_message = ""  # empty response container
            parameters = {
                "model": model,
                "messages": messages,
                "temperature": temperature,
                "top_p": top_p,
                "frequency_penalty": frequency_penalty,
                "presence_penalty": presence_penalty,
                "stream": True
            }

            if max_tokens is not None:
                parameters["max_tokens"] = max_tokens

            response = client.chat.completions.create(**parameters)

            # append new messages to hist
            history = history or []
            history.append({"role": "user", "content": message})
            history.append({"role": "assistant", "content": ""})  # placeholder for assistant's response

            for chunk in response:
                if hasattr(chunk.choices[0].delta, 'content') and chunk.choices[0].delta.content:
                    partial_message += chunk.choices[0].delta.content
                    # update assistant's msh in hist
                    history[-1]['content'] = partial_message
                    # yield updated history & clear txtbox
                    yield history, unique_id_state, ""

            # once response is complete, dwnload history
            download_history(history, unique_id)

        except Exception as e:
            # if error add it to history (so can be seen in form of reply in the ui!)
            history.append({"role": "assistant", "content": f"An error occurred: {str(e)}"})
            yield history, unique_id_state, ""

    def download_history(history, unique_id):
        # convert message history to a list of user-assistant pairs
        history_json = []
        current_pair = {}
        for message in history:
            if message['role'] == 'user':
                current_pair = {'user': message['content']}
            elif message['role'] == 'assistant':
                if 'user' in current_pair:
                    current_pair['assistant'] = message['content']
                    history_json.append(current_pair)
                    current_pair = {}
        # handle any remaining message
        if current_pair:
            history_json.append(current_pair)

        settings = {
            "api_key": api_key,
            "model": model,
            "system_message": system_message,
            "temperature": temperature,
            "max_tokens": max_tokens,
            "top_p": top_p,
            "frequency_penalty": frequency_penalty,
            "presence_penalty": presence_penalty,
            "downloaded_on": datetime.datetime.now().strftime("%B %d, %Y at %H:%M:%S")  # Timestamp
        }

        full_data = {
            "settings": settings,
            "conversation": history_json
        }

        try:
            print("<oAI_v2_gS.py internal> History is being downloaded...")

            script_dir = os.path.dirname(os.path.abspath(__file__))
            chat_dir = os.path.join(script_dir, "chat_histories")
            os.makedirs(chat_dir, exist_ok=True)

            filename = f"chathistory_{unique_id}.json"
            filepath = os.path.join(chat_dir, filename)

            print(f"<oAI_v2_gS.py internal> History is being saved as {filename} in the directory {filepath}.")

            with open(filepath, "w") as f:
                json.dump(full_data, f, indent=4)  # Save settings and history

            print("<oAI_v2_gS.py internal> History was successfully downloaded.")

            return filepath
        except Exception as e:
            return f"Error saving chat history: {str(e)}"

    def create_interface():
        with gr.Blocks() as iface:
            chatbot = gr.Chatbot(type="messages")
            msg = gr.Textbox(label="Send message to the LLM")
            clear = gr.Button("Clear")
            unique_id_label = gr.Label(value="")  # display unique ID gradio
            state = gr.State(value=None)  # unitialize session state for unique ID

            def update_unique_id(state):
                if state is None:
                    state = generate_unique_id()
                return f"Your unique ID: {state}", state

            # update unique ID when iface is loaded
            iface.load(fn=update_unique_id, inputs=state, outputs=[unique_id_label, state])

            # ensure to pass message, history & unique ID state to predict
            msg.submit(predict, inputs=[msg, chatbot, state], outputs=[chatbot, state, msg])
            clear.click(lambda: ([], None, ""), None, [chatbot, state, msg], queue=False)

        return iface

    interface = create_interface()
    interface.launch(share=True) 

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="OpenAI v2 Gradio Server")
    parser.add_argument('--start-gradio', action='store_true', help='Start Gradio Interface')
    parser.add_argument('--api_key', type=str, help='OpenAI API Key')
    parser.add_argument('--model', type=str, default='gpt-3.5-turbo', help='Model name')
    parser.add_argument('--system_message', type=str, default='You are a helpful assistant.', help='System message')
    parser.add_argument('--temperature', type=float, default=0.7, help='Temperature')
    parser.add_argument('--max_tokens', type=int, help='Max tokens (optional)')
    parser.add_argument('--top_p', type=float, default=1.0, help='Top P')
    parser.add_argument('--frequency_penalty', type=float, default=0.0, help='Frequency penalty')
    parser.add_argument('--presence_penalty', type=float, default=0.0, help='Presence penalty')

    args = parser.parse_args()

    if args.start_gradio:
        if not all([
            args.api_key,
            args.model,
            args.system_message is not None,
            args.temperature is not None,
            args.top_p is not None,
            args.frequency_penalty is not None,
            args.presence_penalty is not None
        ]):
            print("<oAI_v2_gS.py internal> Missing parameters for Gradio interface")
            sys.exit(1)

        run_gradio(
            api_key=args.api_key,
            model=args.model,
            system_message=args.system_message,
            temperature=args.temperature,
            max_tokens=args.max_tokens,
            top_p=args.top_p,
            frequency_penalty=args.frequency_penalty,
            presence_penalty=args.presence_penalty
        )
    else:
        print("<oAI_v2_gS.py internal> Gradio interface is not running. Use --start-gradio to start the interface.")
