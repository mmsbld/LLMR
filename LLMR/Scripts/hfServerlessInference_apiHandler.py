import requests
import sys
import argparse

def validate_api_token(api_token):
    test_url = "https://huggingface.co/api/whoami-v2"
    headers = {"Authorization": f"Bearer {api_token}"}
    response = requests.get(test_url, headers=headers)
    return response.status_code == 200

def get_available_models(api_token):
    api_url = "https://huggingface.co/api/models"
    headers = {"Authorization": f"Bearer {api_token}"}
    params = {
        "filter": "llama",
        "sort": "downloads",
        "direction": -1
    }
    response = requests.get(api_url, headers=headers, params=params)
    if response.status_code != 200:
        return []
    models = response.json()
    model_ids = [model["modelId"] for model in models if "llama" in model["modelId"].lower()]
    return model_ids

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Llama API Handler")
    parser.add_argument('--validate-api-token', action='store_true', help='Validate API Token')
    parser.add_argument('--get-available-models', action='store_true', help='Get Available Models')
    parser.add_argument('--api_token', type=str, required=True, help='Hugging Face API Token')

    args = parser.parse_args()

    if args.validate_api_token:
        is_valid = validate_api_token(args.api_token)
        print(f"API Token is valid: {is_valid}")
    elif args.get_available_models:
        models = get_available_models(args.api_token)
        print("Available Llama Models:")
        for model_id in models:
            print(model_id)
    else:
        print("No valid arguments provided.")
