# Virtual Agent Minimal Demo

This project is a minimal demonstration of a virtual agent powered by local AI services, all integrated within a Unity client. It creates an interactive conversational experience by combining real-time Speech-to-Text (STT), a Large Language Model (LLM), and Text-to-Speech (TTS).

## How It Works

The system is composed of several independent services that communicate over the network:

1.  **Speech-to-Text (STT) Server**: Captures audio from the Unity client, transcribes it to text in real-time, and sends it to the LLM.
2.  **Large Language Model (LLM) Server**: Receives the transcribed text, generates a conversational response, and passes it to the TTS server.
3.  **Text-to-Speech (TTS) Server**: Converts the LLM's text response into audible speech.
4.  **Unity Client**: Manages the user interaction, records microphone input, sends it to the STT server, and plays the synthesized audio response from the TTS server.

## Setup Instructions

You need to set up and run three separate servers before launching the Unity application.

### 1. Speech-to-Text (STT) Server

You can choose an OpenAI Whisper-based server or an NVIDIA Parakeet-based server to be the STT server. The whisper-based solution is slower on low-end machines, or on CPU without CUDA support. The parakeet-based solution is optimized for real-time performance on CPU without CUDA, can even achieve better accuracy than whisper Large v3 model. Both of them are integrated with [Silero VAD](https://github.com/snakers4/silero-vad) for voice activity detection.
<details>
<summary>Whisper-based STT</summary>

This uses [RealtimeSTT](https://github.com/stytim/RealtimeSTT-docker) for transcription.

1.  Follow the installation instructions in the repository, I recommend using the Docker for setup.
2.  If you don't want to use Docker, run the Python server using the following command. This will start the server and listen for WebSocket connections for audio data and text output.

```bash
    # -m: model size (e.g., tiny.en)
    # -l: language (e.g., en)
    # -c: text output port
    # -d: audio input port
    python stt_server.py -m tiny.en -l en -c 8011 -d 8012
```

</details>
<details>
<summary>Parakeet-based STT</summary>

This uses [Parakeet-0.6b-v3-fastapi-websocket](https://github.com/stytim/parakeet-tdt-0.6b-v3-fastapi-websocket) for transcription.
1.  Follow the installation instructions in the repository, I recommend using the Docker for setup.
2.  If you don't want to use Docker, run the Python server using the following command. This will start the server and listen for WebSocket connections for audio data and text output.
```bash
    python app.py
```

</details>

### 2. Large Language Model (LLM) Server

The LLM is served using [LlamaLib](https://github.com/undreamai/LlamaLib/releases), a server compatible with the `llama.cpp` ecosystem, but does not support multimodal input. For vision language multimodal support please refer to [vlm branch](https://github.com/stytim/agent-pipeline-minimal/tree/vlm) of this repository.

1.  Download from the LlamaLib releases or build it from source.
2.  Download the GGUF model file you wish to use, I recommend Qwen 3.5 series for state of the art performance, but you can choose any model that fits your needs. You can find the GGUF files for Qwen 3.5 models below:
    - [Qwen 3.5 9B](https://huggingface.co/unsloth/Qwen3.5-9B-GGUF/resolve/main/Qwen3.5-9B-Q4_K_M.gguf)
    - [Qwen 3.5 4B](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/resolve/main/Qwen3.5-4B-Q4_K_M.gguf)
    - [Qwen 3.5 2B](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/Qwen3.5-2B-Q4_K_M.gguf)
    - [Qwen 3.5 0.8B](https://huggingface.co/unsloth/Qwen3.5-0.8B-GGUF/resolve/main/Qwen3.5-0.8B-Q4_K_M.gguf)
3.  Open a terminal and run the LLM server.

The Unity client side has a modified version of [LLM for Unity](https://github.com/undreamai/LLMUnity). Specifically, I removed the dependency of the huge prebuilt library and implemented the C# equivalent of the LlamaLib client, and removed the annoying autodownload of Llamalib in Unity, so be aware if you want to upgrade to the latest LLM for Unity in the future.

**Example: For Qwen-3.5 0.8B on macOS (ARM64):**
```bash
./servers/llamalib_osx-arm64_server -m "Qwen3.5-0.8B-Q4_K_M.gguf" -t -1 -np 1 -c 4096 -b 512 -ngl 99 -fa off --port 13333 --host 0.0.0.0 --verbose
```
For a more detailed explanation of the command-line arguments, please refer to the [llama.cpp documentation](https://github.com/ggml-org/llama.cpp/blob/master/tools/cli/README.md). Here are some key flags to note:

*   **`-m`**: This specifies the path to your GGUF model file.
*   **`--host 0.0.0.0`**: This allows the server to be accessible from other devices on your network.
*   **`--port`**: This sets the port for the server. Make sure it matches the port configured in the Unity client.
*   **`-t`**: This uses all available CPU threads. You can set this to a specific number if you want to limit CPU usage.
*   **`-np`**: This sets the number of parallel clients. Adjust this based on how many simultaneous connections you expect.
*   **`-c`**: This sets the context window size. Larger context windows allow for better understanding of longer conversations but require more memory.
*   **`-b`**: This sets the batch size for processing. Adjust this based on your system's capabilities for optimal performance.
*   **`--ngl 99`**: This flag offloads maximum 99 layers to the GPU. Adjust this number based on your GPU's VRAM. If you encounter errors, try reducing it.
*   **`--fa off`**: This disables the use of flash attention. You can experiment with this setting based on your specific model and hardware.
*   **`--verbose`**: This enables detailed logging, which can be helpful for debugging.

### 3. Text-to-Speech (TTS) Server

You can choose a local kokoro-based server or an online ElevenLabs service to be the TTS service. The kokoro-based solution is more free and can run locally without internet connection, but it requires a NVIDIA GPU for real-time performance. The ElevenLabs solution is paid cloud-based service, it requires an internet connection and has usage limits based on your plan, but has more features, for example, can talk German, and supports voice cloning. For future development, I will integrate QwenTTS, a state of the art local TTS solution, even with very good voice cloning capability that paid ElevenLabs offers.

<details>
<summary>Kokoro-based TTS</summary>

You can first try it out if the voice quality fits your need. I hosted a kokoro TTS server on a website I made: [AI Text to Speech](https://myonlinefiletools.com/tools/text-to-speech). If you want to run it locally, use [Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI), which runs in a Docker container. Follow the instructions in its `README.md` to build and run the Docker container. This will expose the TTS API on your local machine.
</details>

<details>
<summary>ElevenLabs TTS</summary>

1.  Sign up for an account at [ElevenLabs](https://elevenlabs.io/).
2.  Create an API key in your account settings.
3.  Paste the API key into the Unity client configuration located in `Assets/Resources/ElevenLabsConfiguration`. The Unity client will use this key to authenticate requests to the ElevenLabs API. (Make sure to keep your API key secure and do not share it publicly.)
</details>

### 4. Unity Project Setup

Once all servers are running, configure the Unity client.

1.  In the Scene Hierarchy, find the scripts responsible for handling STT, LLM, and TTS (e.g., `STT Handler`, `LLM Handler`, `TTS Handler`).
2.  In the Inspector for each of these objects, update the **IP Address** field to match the IP of the machine running the corresponding server. If all servers are on the same machine as Unity, you can use `localhost`.
3.  In `STT Handler`, select the STT service you set up above in the Active Server Type dropdown.
4.  In `TTS Handler`, select the TTS service you set up above in the Selected TTS dropdown.
5.  Select the `STT Handler` GameObject. In the Inspector, choose the **Microphone** you want to use from the dropdown list.

#### Optional Configurations
-   In STT, you can use the VAD event to trigger agent behaviors. For example, you can configure the behavior triggered when the user starts or stops talking.
-   In LLM, you can adjust the system prompt, as well as the structure output of the LLM response to better fit your use case. The current setup uses a simple JSON format based on grammar file defined at `Assets/StreamingAssets/json.gnbf`, you can read more about how to customize the grammar file in the [here](https://github.com/ggml-org/llama.cpp/tree/master/grammars).
-   In `Conversation Handler`, there is an option to enable Allow Interruption, which will interrupt the TTS audio playback when the user starts talking, and send a stop signal to the LLM server to stop generating response. Because this keeps the microphone unmuted, TTS audio could be captured and forwarded to the STT server, creating a feedback loop. To prevent this, I implemented an [Acoustic Echo Cancellation (AEC) module](https://github.com/stytim/libaec3) based on WebRTC’s AEC3 and built it as a dynamic library for the Unity client. This module effectively removes TTS audio from the microphone input. Enabling this option can make conversations feel more natural, though it may still cause unexpected issues in some cases.

## Running the Demo

1.  Ensure all three backend servers (STT, LLM, TTS) are running without errors.
2.  In the Unity Editor, open the main scene.
3.  Enter Play Mode, the STT, LLM, and TTS indicators should be green.
4.  Click the "Start Conversation" button in the UI.
5.  Begin speaking. You should see your transcribed text and hear the agent's response.
   

## Cite Us
This agent pipeline framework is developed for a series of research projects. If you find this project useful for your research, please consider citing our paper:

```bibtex
@ARTICLE{song2025Enhancing,
  author={Song, Tianyu and Pabst, Felix and Eck, Ulrich and Navab, Nassir},
  journal={IEEE Transactions on Visualization and Computer Graphics}, 
  title={Enhancing Patient Acceptance of Robotic Ultrasound through Conversational Virtual Agent and Immersive Visualizations}, 
  year={2025},
  volume={31},
  number={5},
  pages={2901-2911},
  keywords={Robots;Ultrasonic imaging;Visualization;Mixed reality;Medical services;Virtual assistants;Real-time systems;Virtual environments;Three-dimensional displays;Probes;Mixed Reality;Virtual Agent;Robotic Ultrasound;Trust and Acceptance},
  doi={10.1109/TVCG.2025.3549181}}
```
```bibtex
@inproceedings{song2025intelligent,
  title={Intelligent Virtual Sonographer (IVS): Enhancing Physician-Robot-Patient Communication},
  author={Song, Tianyu and Li, Feng and Bi, Yuan and Karlas, Angelos and Yousefi, Amir and Branzan, Daniela and Jiang, Zhongliang and Eck, Ulrich and Navab, Nassir},
  booktitle={International Conference on Medical Image Computing and Computer-Assisted Intervention},
  pages={287--297},
  year={2025},
  organization={Springer}
}
```