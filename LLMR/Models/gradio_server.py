###########################################################################
###########################################################################
###########################################################################

# not used anylonger (now in Services; 27.09. 16:00):

# import gradio as gr
# import openai
# import json
# import os
# import uuid
# import sys
# import argparse
# import subprocess
# import datetime
# 
# def run_gradio(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
#     client = openai.Client(api_key=api_key)
# 
#     def generate_unique_id():
#         return uuid.uuid4().hex
# 
#     def predict(message, history, unique_id_state):
#         if unique_id_state is None:
#             unique_id = generate_unique_id()
#             unique_id_state = unique_id  # Set the unique ID for the session
#         else:
#             unique_id = unique_id_state  # Retrieve the existing unique ID for the session
#     
#         history_openai_format = []
#         for human, assistant in history:
#             history_openai_format.append({"role": "user", "content": human})
#             history_openai_format.append({"role": "assistant", "content": assistant})
#         history_openai_format.append({"role": "user", "content": message})
#     
#         try:
#             partial_message = ""  # Initialize an empty response container
#             response = client.chat.completions.create(
#                 model=model,
#                 messages=history_openai_format,
#                 temperature=temperature,
#                 max_tokens=max_tokens,
#                 top_p=top_p,
#                 frequency_penalty=frequency_penalty,
#                 presence_penalty=presence_penalty,
#                 stream=True
#             )
#     
#             for chunk in response:
#                 if hasattr(chunk.choices[0].delta, 'content') and chunk.choices[0].delta.content:
#                     partial_message += chunk.choices[0].delta.content
#                     # Yield each partial response as it comes, and clear the textbox
#                     yield history + [(message, partial_message)], unique_id_state, ""
#     
#             # Once the response is complete, download the full history
#             download_history(history + [(message, partial_message)], unique_id)
#     
#         except Exception:
#             # If an error occurs, yield the error message
#             yield history + [(message, "An error occurred. Please try again later.")], unique_id_state, ""
# 
# 
#     def download_history(history, unique_id):
#         history_json = [{"user": human, "assistant": assistant} for human, assistant in history]
#         settings = {
#             "api_key": api_key,
#             "model": model,
#             "temperature": temperature,
#             "max_tokens": max_tokens,
#             "top_p": top_p,
#             "frequency_penalty": frequency_penalty,
#             "presence_penalty": presence_penalty,
#             "downloaded_on": datetime.datetime.now().strftime("%B %d, %Y at %H:%M:%S")  # Zeitstempel
#         }
#         
#         full_data = {
#             "settings": settings,
#             "conversation": history_json
#         }
#     
#         try:
#             print("<GSPY internal> History is being downloaded...")
#     
#             script_dir = os.path.dirname(os.path.abspath(__file__))
#             chat_dir = os.path.join(script_dir, "chat_histories")
#             os.makedirs(chat_dir, exist_ok=True)
#     
#             filename = f"chathistory_{unique_id}.json"
#             filepath = os.path.join(chat_dir, filename)
#     
#             print("<GSPY internal> History is being saved as " + filename + " in the directory " + filepath + ".")
#     
#             with open(filepath, "w") as f:
#                 json.dump(full_data, f, indent=4)  # Speichere Settings und History
#     
#             print("<GSPY internal> History was successfully downloaded.")
#     
#             return filepath
#         except Exception:
#             return "Error saving chat history."
# 
#     def create_interface():
#         with gr.Blocks() as iface:
#             chatbot = gr.Chatbot()
#             msg = gr.Textbox(label="Send message to the LLM")
#             clear = gr.Button("Clear")
#             unique_id_label = gr.Label(value="")  # To display the unique ID
#             state = gr.State(value=None)  # Initialize session state for unique ID
# 
#             def update_unique_id(state):
#                 if state is None:
#                     state = generate_unique_id()
#                 return f"Your unique ID: {state}", state
# 
#             # Update unique ID when the interface is loaded
#             iface.load(fn=update_unique_id, inputs=state, outputs=[unique_id_label, state])
# 
#             # Ensure to pass only message, history, and the unique ID state to predict
#             msg.submit(predict, inputs=[msg, chatbot, state], outputs=[chatbot, state, msg])
#             clear.click(lambda: None, None, chatbot, queue=False)
# 
#         return iface
# 
#     interface = create_interface()
#     interface.launch(share=True)  # Set share=True for public access
# 
# def start_gradio_interface(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
#     global gradio_process
# 
#     if gradio_process is not None and gradio_process.poll() is None:
#         return "Gradio interface is already running."
# 
#     try:
#         cmd = [
#             sys.executable,
#             os.path.join(os.path.dirname(__file__), "gradio_server.py"),
#             "--start-gradio",
#             "--api_key", api_key,
#             "--model", model,
#             "--temperature", str(temperature),
#             "--max_tokens", str(max_tokens),
#             "--top_p", str(top_p),
#             "--frequency_penalty", str(frequency_penalty),
#             "--presence_penalty", str(presence_penalty)
#         ]
#         gradio_process = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
# 
#         stdout, stderr = gradio_process.communicate()
# 
#         return "Gradio interface started."
#     except Exception:
#         return "Error starting Gradio interface."
# 
# def stop_gradio_interface():
#     global gradio_process
# 
#     if gradio_process is None or gradio_process.poll() is not None:
#         return "Gradio interface is not running."
# 
#     try:
#         gradio_process.terminate()
#         gradio_process.wait(timeout=10)
#         gradio_process = None
#         return "Gradio interface stopped."
#     except Exception:
#         return "Error stopping Gradio interface."
# 
# if __name__ == "__main__":
#     parser = argparse.ArgumentParser(description="API Handler")
#     parser.add_argument('--start-gradio', action='store_true', help='Start Gradio Interface')
#     parser.add_argument('--api_key', type=str, help='OpenAI API Key')
#     parser.add_argument('--model', type=str, default='gpt-3.5-turbo', help='Model name')
#     parser.add_argument('--temperature', type=float, default=0.7, help='Temperature')
#     parser.add_argument('--max_tokens', type=int, default=150, help='Max tokens')
#     parser.add_argument('--top_p', type=float, default=1.0, help='Top P')
#     parser.add_argument('--frequency_penalty', type=float, default=0.0, help='Frequency penalty')
#     parser.add_argument('--presence_penalty', type=float, default=0.0, help='Presence penalty')
# 
#     args = parser.parse_args()
# 
#     if args.start_gradio:
#         if not all([
#             args.api_key,
#             args.model,
#             args.temperature is not None,
#             args.max_tokens is not None,
#             args.top_p is not None,
#             args.frequency_penalty is not None,
#             args.presence_penalty is not None
#         ]):
#             print("Missing parameters for Gradio interface")
#             sys.exit(1)
#         
#         run_gradio(
#             api_key=args.api_key,
#             model=args.model,
#             temperature=args.temperature,
#             max_tokens=args.max_tokens,
#             top_p=args.top_p,
#             frequency_penalty=args.frequency_penalty,
#             presence_penalty=args.presence_penalty
#         )
#     else:
#         print("API Handler is not running as a Gradio interface.")


###########################################################################
###########################################################################
###########################################################################
