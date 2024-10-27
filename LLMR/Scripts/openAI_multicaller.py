import argparse
import json
import os
import uuid
import datetime
from openai import OpenAI

def run_multicaller(api_key, model, prompt, n, system_message, temperature, max_tokens, top_p, frequency_penalty, presence_penalty):
    client = OpenAI(api_key=api_key)

    messages = [
        {"role": "system", "content": system_message},
        {"role": "user", "content": prompt}
    ]

    unique_id = uuid.uuid4().hex

    responses = []

    for i in range(n):
        try:
            parameters = {
                "model": model,
                "messages": messages,
                "temperature": temperature,
                "top_p": top_p,
                "frequency_penalty": frequency_penalty,
                "presence_penalty": presence_penalty
            }

            if max_tokens is not None:
                parameters["max_tokens"] = max_tokens

            completion = client.chat.completions.create(**parameters)

            assistant_reply = completion.choices[0].message.content

            responses.append({
                "attempt": i + 1,
                "assistant": assistant_reply
            })

            print(f"Completed call {i + 1}/{n}")

        except Exception as e:
            responses.append({
                "attempt": i + 1,
                "error": str(e)
            })
            print(f"Error on call {i + 1}/{n}: {str(e)}")

    save_results(unique_id, api_key, model, system_message, prompt, n, temperature, max_tokens, top_p, frequency_penalty, presence_penalty, responses)

def save_results(unique_id, api_key, model, system_message, prompt, n, temperature, max_tokens, top_p, frequency_penalty, presence_penalty, responses):
    settings = {
        "api_key": api_key,
        "model": model,
        "system_message": system_message,
        "prompt": prompt,
        "n": n,
        "temperature": temperature,
        "max_tokens": max_tokens,
        "top_p": top_p,
        "frequency_penalty": frequency_penalty,
        "presence_penalty": presence_penalty,
        "downloaded_on": datetime.datetime.now().strftime("%B %d, %Y at %H:%M:%S")
    }

    data = {
        "settings": settings,
        "responses": responses
    }

    try:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        chat_dir = os.path.join(script_dir, "chat_histories", "Multicaller")
        os.makedirs(chat_dir, exist_ok=True)

        filename = f"multicaller_{unique_id}.json"
        filepath = os.path.join(chat_dir, filename)

        with open(filepath, "w") as f:
            json.dump(data, f, indent=4)

        print(f"Results saved to {filepath}")

    except Exception as e:
        print(f"Error saving results: {str(e)}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="OpenAI Multicaller")
    parser.add_argument('--api_key', type=str, required=True, help='OpenAI API Key')
    parser.add_argument('--model', type=str, default='gpt-4o', help='Model name')
    parser.add_argument('--prompt', type=str, required=True, help='User prompt')
    parser.add_argument('--n', type=int, default=5, help='Number of API calls')
    parser.add_argument('--system_message', type=str, default='You are a helpful assistant.', help='System message')
    parser.add_argument('--temperature', type=float, default=1, help='Temperature')
    parser.add_argument('--max_tokens', type=int, help='Max tokens (optional)')
    parser.add_argument('--top_p', type=float, default=1, help='Top P')
    parser.add_argument('--frequency_penalty', type=float, default=0, help='Frequency penalty')
    parser.add_argument('--presence_penalty', type=float, default=0, help='Presence penalty')

    args = parser.parse_args()

    run_multicaller(
        api_key=args.api_key,
        model=args.model,
        prompt=args.prompt,
        n=args.n,
        system_message=args.system_message,
        temperature=args.temperature,
        max_tokens=args.max_tokens,
        top_p=args.top_p,
        frequency_penalty=args.frequency_penalty,
        presence_penalty=args.presence_penalty
    )
