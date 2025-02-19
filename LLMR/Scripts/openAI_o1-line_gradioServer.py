import gradio as gr
import openai
import json
import os
import uuid
import sys
import argparse
import datetime

def run_gradio(api_key, model, reasoning_effort, max_completion_tokens):
    client = openai.Client(api_key=api_key)

    def generate_unique_id():
        return uuid.uuid4().hex

    def predict(message, history, unique_id_state):
        if unique_id_state is None:
            unique_id = generate_unique_id()
            unique_id_state = unique_id  
        else:
            unique_id = unique_id_state  

        # conversation history without system message
        messages = []
        if history:
            messages.extend(history)
        messages.append({"role": "user", "content": message})

        try:
            partial_message = ""  # container for streamed response
            parameters = {
                "model": model,
                "messages": messages,
                "stream": True
            }
            if reasoning_effort is not None:
                parameters["reasoning_effort"] = reasoning_effort

            if max_completion_tokens is not None:
                parameters["max_completion_tokens"] = max_completion_tokens

            response = client.chat.completions.create(**parameters)

            # Append new user message & add placeholder for the assistant’s reply
            history = history or []
            history.append({"role": "user", "content": message})
            history.append({"role": "assistant", "content": ""})

            for chunk in response:
                if hasattr(chunk.choices[0].delta, 'content') and chunk.choices[0].delta.content:
                    partial_message += chunk.choices[0].delta.content
                    history[-1]['content'] = partial_message
                    # Yield updated history and clear the textbox
                    yield history, unique_id_state, ""

            # Once the response is complete, download the chat history
            download_history(history, unique_id)

        except Exception as e:
            # If error: add it to the chat history so it appears in the UI window (gradio interface / chathistory in UI on LLMR server)
            history.append({"role": "assistant", "content": f"An error occurred: {str(e)}"})
            yield history, unique_id_state, ""

    def download_history(history, unique_id):
        # Convert chat history to list of user-assi pairs
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
        if current_pair:
            history_json.append(current_pair)

        settings = {
            "api_key": api_key,
            "model": model,
            "reasoning_effort": reasoning_effort,
            "max_completion_tokens": max_completion_tokens,
            "downloaded_on": datetime.datetime.now().strftime("%B %d, %Y at %H:%M:%S")
        }

        full_data = {
            "settings": settings,
            "conversation": history_json
        }

        try:
            print("<oAI_o1_gS.py internal> History is being downloaded...")

            script_dir = os.path.dirname(os.path.abspath(__file__))
            chat_dir = os.path.join(script_dir, "chat_histories")
            os.makedirs(chat_dir, exist_ok=True)

            filename = f"chathistory_{unique_id}.json"
            filepath = os.path.join(chat_dir, filename)

            print(f"<oAI_o1_gS.py internal> History is being saved as {filename} in the directory {filepath}.")

            with open(filepath, "w") as f:
                json.dump(full_data, f, indent=4)

            print("<oAI_o1_gS.py internal> History was successfully downloaded.")
            return filepath
        except Exception as e:
            return f"Error saving chat history: {str(e)}"

    def create_interface():
        with gr.Blocks() as iface:
            chatbot = gr.Chatbot(type="messages")
            msg = gr.Textbox(label="Send message to the LLM")
            clear = gr.Button("Clear")
            unique_id_label = gr.Label(value="")  # displays the unique session ID (this is still far too large! in ToDo!)
            state = gr.State(value=None)  # uninitialized session state for unique ID

            def update_unique_id(state):
                if state is None:
                    state = generate_unique_id()
                return f"Your unique ID: {state}", state

            # Update unique ID when interface is fully loaded
            iface.load(fn=update_unique_id, inputs=state, outputs=[unique_id_label, state])

            # Pass message, history, and unique ID state to predict in py script
            msg.submit(predict, inputs=[msg, chatbot, state], outputs=[chatbot, state, msg])
            clear.click(lambda: ([], None, ""), None, [chatbot, state, msg], queue=False)

        return iface

    interface = create_interface()
    interface.launch(share=True) 

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="OpenAI o1-line Gradio Server")
    parser.add_argument('--start-gradio', action='store_true', help='Start Gradio Interface')
    parser.add_argument('--api_key', type=str, help='OpenAI API Key')
    parser.add_argument('--model', type=str, default='o1', help='Model name for o1-line usage')
    parser.add_argument('--reasoning_effort', type=str, choices=['low', 'medium', 'high'], help='Reasoning effort (low, medium, or high)')    
    parser.add_argument('--max_completion_tokens', type=int, help='Max completion tokens (optional)')

    args = parser.parse_args()

    if args.start_gradio:
        if not all([
            args.api_key,
            args.model is not None
        ]):
            print("<oAI_o1_gS.py internal> Missing parameters for Gradio interface")
            sys.exit(1)

        run_gradio(
            api_key=args.api_key,
            model=args.model,
            reasoning_effort=args.reasoning_effort,
            max_completion_tokens=args.max_completion_tokens
        )
    else:
        print("<oAI_o1_gS.py internal> Gradio interface is not running. Use --start-gradio to start the interface.")
