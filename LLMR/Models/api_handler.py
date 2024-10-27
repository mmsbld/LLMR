
###########################################################################
###########################################################################
###########################################################################

# not used anylonger (now in Services; 27.09. 16:00):

# Models/api_handler.py

# import sys
# import os
# import openai
# import json
# import logging
# import uuid
# 
# # Logging konfigurieren
# logging.basicConfig(level=logging.DEBUG)
# 
# # Bestimmen Sie den absoluten Pfad zum 'Models'-Verzeichnis
# current_dir = os.path.dirname(os.path.abspath(__file__))
# models_path = os.path.join(current_dir, "Models")
# 
# sys.path.append(models_path)
# 
# def validate_api_key(api_key):
#     """
#     Validiert den OpenAI API-Schlüssel.
#     """
#     try:
#         client = openai.OpenAI(api_key=api_key)
#         response = client.models.list()
#         logging.debug("API-Schlüssel validiert und OpenAI-Client erfolgreich initialisiert.")
#         return True
#     except Exception as e:
#         logging.error(f"Fehler bei der Validierung des API-Schlüssels: {e}")
#         return False
# 
# def get_available_models(api_key):
#     """
#     Gibt eine Liste der verfügbaren OpenAI-Modelle zurück.
#     """
#     try:
#         client = openai.OpenAI(api_key=api_key)
#         response = client.models.list()
#         models = [model.id for model in response]
#         logging.debug(f"Verfügbare Modelle: {models}")
#         return models
#     except Exception as e:
#         logging.error(f"Fehler beim Abrufen der verfügbaren Modelle: {e}")
#         return []


###########################################################################
###########################################################################
###########################################################################

# import gradio as gr
# import openai
# import json
# import logging
# import os
# import uuid
# import threading
# import signal
# import subprocess
# import sys
# import argparse
# 
# # Logging konfigurieren
# logging.basicConfig(level=logging.DEBUG)
# 
# # Globale Variable zur Verwaltung des Gradio-Prozesses
# gradio_process = None
# 
# def validate_api_key(api_key):
#     """
#     Validiert den OpenAI API-Schlüssel.
#     """
#     try:
#         client = openai.OpenAI(api_key=api_key)
#         response = client.models.list()
#         logging.debug("API-Schlüssel validiert und OpenAI-Client erfolgreich initialisiert.")
#         return True
#     except Exception as e:
#         logging.error(f"Fehler bei der Validierung des API-Schlüssels: {e}")
#         return False
# 
# def get_available_models(api_key):
#     """
#     Gibt eine Liste der verfügbaren OpenAI-Modelle zurück.
#     """
#     try:
#         client = openai.OpenAI(api_key=api_key)
#         response = client.models.list()
#         models = [model.id for model in response]
#         logging.debug(f"Verfügbare Modelle: {models}")
#         return models
#     except Exception as e:
#         logging.error(f"Fehler beim Abrufen der verfügbaren Modelle: {e}")
#         return []
# 
# def start_gradio_interface(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
#     """
#     Startet das Gradio-Interface in einem separaten Prozess.
#     """
#     global gradio_process
# 
#     if gradio_process is not None and gradio_process.poll() is None:
#         logging.warning("Gradio-Interface läuft bereits.")
#         return "Gradio-Interface läuft bereits."
# 
#     try:
#         # Starte das gleiche Skript mit dem Flag '--start-gradio' und den notwendigen Argumenten
#         cmd = [
#             sys.executable,        # Pfad zum Python-Interpreter
#             __file__,              # Pfad zu diesem Skript
#             "--start-gradio",
#             api_key,
#             model,
#             str(temperature),
#             str(max_tokens),
#             str(top_p),
#             str(frequency_penalty),
#             str(presence_penalty)
#         ]
#         gradio_process = subprocess.Popen(cmd)
#         logging.debug("Gradio-Interface erfolgreich gestartet.")
#         return "Gradio-Interface gestartet."
#     except Exception as e:
#         logging.error(f"Fehler beim Starten des Gradio-Interfaces: {e}")
#         return "Fehler beim Starten des Gradio-Interfaces."
# 
# def stop_gradio_interface():
#     """
#     Stoppt das Gradio-Interface, indem der Gradio-Prozess terminiert wird.
#     """
#     global gradio_process
# 
#     if gradio_process is None or gradio_process.poll() is not None:
#         logging.warning("Gradio-Interface läuft nicht.")
#         return "Gradio-Interface läuft nicht."
# 
#     try:
#         logging.debug("Gradio-Interface wird gestoppt.")
#         gradio_process.terminate()
#         gradio_process.wait(timeout=10)
#         gradio_process = None
#         logging.debug("Gradio-Interface erfolgreich gestoppt.")
#         return "Gradio-Interface gestoppt."
#     except Exception as e:
#         logging.error(f"Fehler beim Stoppen des Gradio-Interfaces: {e}")
#         return "Fehler beim Stoppen des Gradio-Interfaces."
# 
# def run_gradio(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
#     """
#     Führt das Gradio-Interface aus.
#     """
#     # OpenAI-Client initialisieren
#     try:
#         client = openai.OpenAI(api_key=api_key)
#         logging.debug("OpenAI-Client erfolgreich initialisiert.")
#     except AttributeError as e:
#         logging.error(f"Fehler bei der Initialisierung des OpenAI-Clients: {e}")
#         return
#     except Exception as e:
#         logging.error(f"Allgemeiner Fehler bei der Initialisierung des OpenAI-Clients: {e}")
#         return
# 
#     # Funktion zur Kommunikation mit dem OpenAI LLM mit Streaming und JSON-History
#     def predict(message, history):
#         logging.debug(f"predict aufgerufen mit message: {message}")
#         history_openai_format = []
#         for human, assistant in history:
#             history_openai_format.append({"role": "user", "content": human})
#             history_openai_format.append({"role": "assistant", "content": assistant})
#         history_openai_format.append({"role": "user", "content": message})
# 
#         try:
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
#             partial_message = ""
#             for chunk in response:
#                 if hasattr(chunk.choices[0].delta, 'content') and chunk.choices[0].delta.content:
#                     partial_message += chunk.choices[0].delta.content
#                     yield history + [(message, partial_message)]
#         except Exception as e:
#             logging.error(f"Fehler in predict: {e}")
#             yield history + [(message, "Ein Fehler ist aufgetreten. Bitte versuchen Sie es später erneut.")]
# 
#     # Funktion zum Herunterladen des Chatverlaufs als JSON-Datei
#     def download_history(history):
#         logging.debug("download_history aufgerufen")
#         history_json = [{"user": human, "assistant": assistant} for human, assistant in history]
#         try:
#             # Erstellen des Ordners, falls er nicht existiert
#             script_dir = os.path.dirname(os.path.abspath(__file__))
#             chat_dir = os.path.join(script_dir, "chat_histories")
#             os.makedirs(chat_dir, exist_ok=True)
#             
#             # Generieren eines eindeutigen Dateinamens
#             unique_id = uuid.uuid4().hex
#             filename = f"chathistory_{unique_id}.json"
#             filepath = os.path.join(chat_dir, filename)
#             
#             with open(filepath, "w") as f:
#                 json.dump(history_json, f, indent=4)
#             
#             logging.debug(f"Chatverlauf erfolgreich gespeichert als {filepath}.")
#             return filepath
#         except Exception as e:
#             logging.error(f"Fehler beim Speichern des Chatverlaufs: {e}")
#             return "Fehler beim Speichern des Chatverlaufs."
# 
#     # Erstellen des Gradio-Chat-Interfaces
#     with gr.Blocks() as iface:
#         chatbot = gr.Chatbot()
#         msg = gr.Textbox(label="Nachricht an das LLM senden")
#         clear = gr.Button("Löschen")
#         download_button = gr.Button("Chatverlauf herunterladen")
# 
#         msg.submit(predict, [msg, chatbot], chatbot)
#         clear.click(lambda: None, None, chatbot, queue=False)
#         download_button.click(download_history, inputs=[chatbot], outputs=gr.File())
# 
#     # Launch des Gradio-Interfaces
#     try:
#         logging.debug("Gradio-Interface wird gestartet mit interface.launch")
#         share_url = iface.launch(share=True)
#         logging.debug(f"Gradio-Interface gestartet unter: {share_url}")
#         print(f"Gradio-Interface gestartet unter: {share_url}")
#     except Exception as e:
#         logging.error(f"Fehler beim Starten des Gradio-Servers: {e}")
# 
# # Optional: Wenn das Skript direkt ausgeführt wird, kann es getestet werden
# if __name__ == "__main__":
#     parser = argparse.ArgumentParser(description="API Handler")
#     parser.add_argument('--start-gradio', action='store_true', help='Start Gradio Interface')
#     parser.add_argument('api_key', nargs='?', help='OpenAI API Key')
#     parser.add_argument('model', nargs='?', help='Model name')
#     parser.add_argument('temperature', nargs='?', type=float, help='Temperature')
#     parser.add_argument('max_tokens', nargs='?', type=int, help='Max tokens')
#     parser.add_argument('top_p', nargs='?', type=float, help='Top P')
#     parser.add_argument('frequency_penalty', nargs='?', type=float, help='Frequency penalty')
#     parser.add_argument('presence_penalty', nargs='?', type=float, help='Presence penalty')
# 
#     args = parser.parse_args()
# 
#     if args.start_gradio:
#         # Überprüfen Sie, ob alle erforderlichen Parameter vorhanden sind
#         if not all([
#             args.api_key,
#             args.model,
#             args.temperature is not None,
#             args.max_tokens is not None,
#             args.top_p is not None,
#             args.frequency_penalty is not None,
#             args.presence_penalty is not None
#         ]):
#             print("Fehlende Parameter für Gradio-Interface")
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
#         print("API Handler läuft nicht als Gradio-Interface.")
# 

# ##################
# # bevor api_handler.py und gradio_server.py zusammengeworfen:
# ##################
# # import sys
# # import os
# # import openai
# # 
# # # Bestimmen Sie den absoluten Pfad zum 'Models'-Verzeichnis
# # current_dir = os.path.dirname(os.path.abspath(__file__))
# # models_path = os.path.join(current_dir, "Models")
# # 
# # sys.path.append(models_path)
# # 
# # def validate_api_key(api_key):
# #     client = openai.OpenAI(api_key=api_key)
# #     response = client.models.list()
# #     return True
# # 
# # def get_available_models(api_key):
# #     client = openai.OpenAI(api_key=api_key)
# #     response = client.models.list()
# #     models = [model.id for model in response]
# #     return models
# 
# 
# import sys
# import os
# import openai
# import json
# import logging
# import uuid
# import threading
# import signal
# import gradio as gr
# 
# # # Logging konfigurieren
# # logging.basicConfig(level=logging.DEBUG)
# # 
# # # Globale Variablen für das OpenAI-Modell und Gradio-Interface
# # client = None
# # gradio_thread = None
# # gradio_running = False
# # gradio_lock = threading.Lock()
# 
# current_dir = os.path.dirname(os.path.abspath(__file__))
# models_path = os.path.join(current_dir, "Models")
# 
# sys.path.append(models_path)
# 
# def validate_api_key(api_key):
#     client = openai.OpenAI(api_key=api_key)
#     response = client.models.list()
#     return True
# 
# def get_available_models(api_key):
#     client = openai.OpenAI(api_key=api_key)
#     response = client.models.list()
#     models = [model.id for model in response]
#     return models
# 
# # def validate_api_key(api_key):
# #     """
# #     Validiert den OpenAI API-Schlüssel.
# #     """
# #     try:
# #         client = openai.OpenAI(api_key=api_key)
# #         response = client.models.list()
# #         logging.debug("API-Schlüssel validiert und OpenAI-Client erfolgreich initialisiert.")
# #         return True
# #     except Exception as e:
# #         logging.error(f"Fehler bei der Validierung des API-Schlüssels: {e}")
# #         return False
# # 
# # def get_available_models(api_key):
# #     """
# #     Gibt eine Liste der verfügbaren OpenAI-Modelle zurück.
# #     """
# #     try:
# #         client = openai.OpenAI(api_key=api_key)
# #         response = client.models.list()
# #         models = [model.id for model in response]
# #         logging.debug(f"Verfügbare Modelle: {models}")
# #         return models
# #     except Exception as e:
# #         logging.error(f"Fehler beim Abrufen der verfügbaren Modelle: {e}")
# #         return []
# 
# def start_gradio_interface(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
#     """
#     Startet das Gradio-Interface in einem separaten Thread.
#     """
#     global client, gradio_thread, gradio_running
# 
#     with gradio_lock:
#         if gradio_running:
#             logging.warning("Gradio-Interface läuft bereits.")
#             return "Gradio-Interface läuft bereits."
# 
#         # OpenAI-Client initialisieren
#         try:
#             client = openai.OpenAI(api_key=api_key)
#             logging.debug("OpenAI-Client erfolgreich initialisiert.")
#         except AttributeError as e:
#             logging.error(f"Fehler bei der Initialisierung des OpenAI-Clients: {e}")
#             return "Fehler bei der Initialisierung des OpenAI-Clients."
#         except Exception as e:
#             logging.error(f"Allgemeiner Fehler bei der Initialisierung des OpenAI-Clients: {e}")
#             return "Allgemeiner Fehler bei der Initialisierung des OpenAI-Clients."
# 
#         gradio_running = True
# 
#     # Funktion zur Kommunikation mit dem OpenAI LLM mit Streaming und JSON-History
#     def predict(message, history):
#         logging.debug(f"predict aufgerufen mit message: {message}")
#         history_openai_format = []
#         for human, assistant in history:
#             history_openai_format.append({"role": "user", "content": human})
#             history_openai_format.append({"role": "assistant", "content": assistant})
#         history_openai_format.append({"role": "user", "content": message})
# 
#         try:
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
#             partial_message = ""
#             for chunk in response:
#                 if hasattr(chunk.choices[0].delta, 'content') and chunk.choices[0].delta.content:
#                     partial_message += chunk.choices[0].delta.content
#                     yield history + [(message, partial_message)]
#         except Exception as e:
#             logging.error(f"Fehler in predict: {e}")
#             yield history + [(message, "Ein Fehler ist aufgetreten. Bitte versuchen Sie es später erneut.")]
# 
#     # Funktion zum Herunterladen des Chatverlaufs als JSON-Datei
#     def download_history(history):
#         logging.debug("download_history aufgerufen")
#         history_json = [{"user": human, "assistant": assistant} for human, assistant in history]
#         try:
#             # Erstellen des Ordners, falls er nicht existiert
#             script_dir = os.path.dirname(os.path.abspath(__file__))
#             chat_dir = os.path.join(script_dir, "chat_histories")
#             os.makedirs(chat_dir, exist_ok=True)
#             
#             # Generieren eines eindeutigen Dateinamens
#             unique_id = uuid.uuid4().hex
#             filename = f"chathistory_{unique_id}.json"
#             filepath = os.path.join(chat_dir, filename)
#             
#             with open(filepath, "w") as f:
#                 json.dump(history_json, f, indent=4)
#             
#             logging.debug(f"Chatverlauf erfolgreich gespeichert als {filepath}.")
#             return filepath
#         except Exception as e:
#             logging.error(f"Fehler beim Speichern des Chatverlaufs: {e}")
#             return "Fehler beim Speichern des Chatverlaufs."
# 
#     # Erstellen des Gradio-Chat-Interfaces
#     def create_interface():
#         with gr.Blocks() as iface:
#             chatbot = gr.Chatbot()
#             msg = gr.Textbox(label="Nachricht an das LLM senden")
#             clear = gr.Button("Löschen")
#             download_button = gr.Button("Chatverlauf herunterladen")
# 
#             msg.submit(predict, [msg, chatbot], chatbot)
#             clear.click(lambda: None, None, chatbot, queue=False)
#             download_button.click(download_history, inputs=[chatbot], outputs=gr.File())
# 
#         return iface
# 
#     interface = create_interface()
# 
#     # Funktion zum Starten des Gradio-Servers
#     def run_gradio():
#         global gradio_running
#         try:
#             logging.debug("Gradio-Interface wird gestartet mit interface.launch")
#             share_url = interface.launch(share=True, block=True)
#             logging.debug(f"Gradio-Interface gestartet unter: {share_url}")
#             print(f"Gradio-Interface gestartet unter: {share_url}")
#         except Exception as e:
#             logging.error(f"Fehler beim Starten des Gradio-Servers: {e}")
#         finally:
#             with gradio_lock:
#                 gradio_running = False
# 
#     # Start Gradio in einem neuen Thread
#     gradio_thread = threading.Thread(target=run_gradio, daemon=True)
#     gradio_thread.start()
# 
#     return "Gradio-Interface gestartet"
# 
# def stop_gradio_interface():
#     """
#     Stoppt das Gradio-Interface, indem der Python-Prozess terminiert wird.
#     Achtung: Dies beendet den gesamten Python-Prozess.
#     """
#     global gradio_thread, gradio_running
# 
#     with gradio_lock:
#         if not gradio_running or gradio_thread is None:
#             logging.warning("Gradio-Interface läuft nicht.")
#             return "Gradio-Interface läuft nicht."
# 
#         try:
#             # Gradio bietet keine direkte Methode zum Stoppen des Interfaces.
#             # Daher wird der gesamte Python-Prozess beendet.
#             logging.debug("Gradio-Interface wird gestoppt.")
#             os.kill(os.getpid(), signal.SIGTERM)
#             gradio_running = False
#             return "Gradio-Interface gestoppt."
#         except Exception as e:
#             logging.error(f"Fehler beim Stoppen des Gradio-Interfaces: {e}")
#             return "Fehler beim Stoppen des Gradio-Interfaces."
# 
# # # Optional: Wenn das Skript direkt ausgeführt wird, kann es getestet werden
# # if __name__ == "__main__":
# #     # Beispielaufruf zum Starten des Gradio-Interfaces
# #     # Ersetzen Sie 'sk-...' mit Ihrem tatsächlichen OpenAI-API-Schlüssel
# #     result = start_gradio_interface(
# #         api_key="sk-...",  # Ersetzen Sie dies mit Ihrem tatsächlichen API-Schlüssel
# #         model="gpt-3.5-turbo",
# #         temperature=0.7,
# #         max_tokens=150,
# #         top_p=1.0,
# #         frequency_penalty=0.0,
# #         presence_penalty=0.0
# #     )
# #     print(result)
# 
#     # Optional: Stoppen des Gradio-Interfaces nach einer bestimmten Zeit
#     # import time
#     # time.sleep(60)  # Warte 60 Sekunden
#     # result = stop_gradio_interface()
#     # print(result)
# 
# 
# 
# # ist das möglicherweise das Problem, dass zum StackOverflow führt?
# # def start_gradio_interface(api_key, model, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
# #     # Importieren Sie gradio_server innerhalb der Funktion, um rekursive Importe zu vermeiden
# #     import gradio_server
# #     return gradio_server.start_gradio_interface(
# #         api_key,
# #         model,
# #         temperature,
# #         max_tokens,
# #         top_p,
# #         frequency_penalty,
# #         presence_penalty
# #     )
# # 
# # def stop_gradio_interface():
# #     # Importieren Sie gradio_server innerhalb der Funktion, um rekursive Importe zu vermeiden
# #     import gradio_server
# #     gradio_server.stop_gradio_interface()
