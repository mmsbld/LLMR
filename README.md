<!-- 
README.md

This README serves as the main landing page for our project. Later, when we switch the repository to public, this should provide an overview of the application, download links for various platforms, and links to documentation files.
-->

<div align="center">
  <picture>
    <source srcset="LLMR/Assets/logo/logo_full.png" media="(prefers-color-scheme: dark)">
    <img src="LLMR/Assets/logo/logo_full.png" alt="LLMR Logo" width="50%">
  </picture>
</div>

# LLMRunner

*Effortless Model Deployment and Customization*

---

LLMR is an open-source application for empirically investigating interactions with various large language models (LLMs). As a client-server solution, LLMR offers easy access to a variety of different LLMs. Subjects can chat with the LLM directly via a browser-based chat interface without having to register. All chat transcripts are automatically recorded, clearly managed in the integrated explorer and can be exported at any time. 


<div align="center">
  <img src="LLMR/Assets/GIFs/moduleSelection.gif" alt="Model Selection" width="35%" style="margin: 20px;">
  <!--  <img src="LLMR/Assets/GIFs/openAiModelSelection.gif" alt="OpenAI Model Selection" width="35%" style="margin: 20px;"> -->
</div>

LLMR integrates seamlessly with both Hugging Face’s Serverless Inference API and OpenAI’s API, providing access to a wide range of models, including the latest versions of ChatGPT. With LLMR, you can fine-tune settings to optimize performance and output for specific scenarios with ease.

## 🔍 How It Works  
LLMR operates as a server running locally on the researcher’s machine. Once a model is selected, students, pupils, and other participants can access the chat interface via a **public link—no registration or login required**.

---

## 🚀 A Student Project – aka. *"Why Is This Still Pre-Alpha?"*  
Before you judge our code too harshly, please consider our humungous core development team that consists of exactly **two** people:  

👨‍💻 **Moritz Seibold** – Development, Co-Testing, Planning, Coordinating, and whatever else was necessary  
🧑‍💻 **Jan Kodweiß** – Testing, Co-Development, Planning, Coordinating, and desperately searching for missing semicolons  

We are both **Mathematics students**, and while we love spending thoughts on how generative AI might (not) influence Mathematics Ed., occasionally we still lose battles against **multivariate calculus**. So, instead of blaming us for missing **unit tests** or **bugs**, we kindly ask you to first take a look at [how innocent we look!](LLMR/Assets/GIFs/janMoritz.png). If you still feel the urge to complain after that, feel free to contact us or contribute your fixes directly. 

---

## 🛠️ Before the Big Presentation...  
Before we unleash LLMR onto an unsuspecting audience at an upcoming conference (with a **complete** documentation 📄), here are some major things we are working on currently:  

✅ **Refactoring** – Cleaning up our code, reducing redundancy with a focus on the wabbly marriage between Python and .net 
✅ **Unit Testing** – Because at some point, we need to test things properly... probably  
✅ **Improved UI/UX** – More features, more UX improvements, and fewer *“Oops, something went wrong”* messages  
✅ **Customizable Export Options** – And a choice: CSV, JSON, maybe even an old-school **TXT file** for the nostalgic ones among us  

Will we finish all of these until ...? **Unlikely.** But we’ll give it our best shot and announce the first non-pre alpha proudly here on this page! 😉  

---

## 📌 About  
**LLMRunner (LLMR)** is an open-source client-server application designed to explore human interaction with large language models in education. With support for **Hugging Face’s Serverless Inference API** and **OpenAI’s API**, LLMR allows easy access to a variety of **state-of-the-art LLMs** via a simple, browser-based interface!  

