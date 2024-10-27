import sys
import os
import openai
import json
import logging
import uuid

logging.basicConfig(level=logging.DEBUG)

current_dir = os.path.dirname(os.path.abspath(__file__))
models_path = os.path.join(current_dir, "Models")

sys.path.append(models_path)

def validate_api_key(api_key):
    try:
        client = openai.OpenAI(api_key=api_key)
        response = client.models.list()
        logging.debug("API-Schlüssel validiert und OpenAI-Client erfolgreich initialisiert.")
        return True
    except Exception as e:
        logging.error(f"Fehler bei der Validierung des API-Schlüssels: {e}")
        return False

def get_available_models(api_key):
    try:
        client = openai.OpenAI(api_key=api_key)
        response = client.models.list()
        models = [model.id for model in response]
        logging.debug(f"Verfügbare Modelle: {models}")
        return models
    except Exception as e:
        logging.error(f"Fehler beim Abrufen der verfügbaren Modelle: {e}")
        return []