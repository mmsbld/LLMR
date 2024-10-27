import sys
import os
import openai
import json
import logging
import uuid

# configure logging
logging.basicConfig(level=logging.DEBUG)

def validate_api_key(api_key):
    try:
        client = openai.OpenAI(api_key=api_key)
        response = client.models.list()
        logging.debug("API key validated and models retrieved.")
        return True
    except Exception as e:
        logging.error(f"Error with the validation of the api key: {e}")
        return False

def get_available_models(api_key):
    try:
        client = openai.OpenAI(api_key=api_key)
        response = client.models.list()
        models = [model.id for model in response]
        logging.debug(f"Available models: {models}")
        return models
    except Exception as e:
        logging.error(f"Error while retrieving models: {e}")
        return []
        