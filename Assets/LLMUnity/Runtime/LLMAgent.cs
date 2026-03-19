/// @file
/// @brief File implementing the LLM chat agent functionality for Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UndreamAI.LlamaLib;
using UnityEngine;

namespace LLMUnity
{
    [DefaultExecutionOrder(-1)]
    /// @ingroup llm
    /// <summary>
    /// Unity MonoBehaviour that implements a conversational AI agent with persistent chat history.
    /// Extends LLMClient to provide chat-specific functionality including role management,
    /// conversation history persistence, and specialized chat completion methods.
    /// </summary>
    public class LLMAgent : LLMClient
    {
        #region Inspector Fields
        /// <summary>Filename for saving chat history (saved in persistentDataPath)</summary>
        [Tooltip("Filename for saving chat history (saved in persistentDataPath)")]
        [LLM] public string save = "";

        /// <summary>Debug LLM prompts</summary>
        [Tooltip("Debug LLM prompts")]
        [LLM] public bool debugPrompt = false;

        /// <summary>Server slot to use for processing (affects caching behavior)</summary>
        [Tooltip("Server slot to use for processing (affects caching behavior)")]
        [ModelAdvanced, SerializeField] protected int _slot = -1;

        /// <summary>System prompt that defines the AI's personality and behavior</summary>
        [TextArea(5, 10), Chat, SerializeField]
        [Tooltip("System prompt that defines the AI's personality and behavior")]
        protected string _systemPrompt = "A chat between a curious human and an artificial intelligence assistant. The assistant gives helpful, detailed, and polite answers to the human's questions.";
        #endregion

        #region Public Properties
        /// <summary>Server slot ID for this agent's requests</summary>
        public int slot
        {
            get => _slot;
            set
            {
                if (_slot != value)
                {
                    _slot = value;
                    if (llmAgent != null) llmAgent.SlotId = _slot;
                    if (pureRemoteAgent != null) pureRemoteAgent.SlotId = _slot;
                }
            }
        }

        /// <summary>System prompt defining the agent's behavior and personality</summary>
        public string systemPrompt
        {
            get => _systemPrompt;
            set
            {
                if (_systemPrompt != value)
                {
                    _systemPrompt = value;
                    if (llmAgent != null) llmAgent.SystemPrompt = _systemPrompt;
                    if (pureRemoteAgent != null) pureRemoteAgent.SystemPrompt = _systemPrompt;
                }
            }
        }

        /// <summary>The underlying LLMAgent instance from LlamaLib</summary>
        public UndreamAI.LlamaLib.LLMAgent llmAgent { get; protected set; }

        /// <summary>Pure C# remote agent used when native remote mode is disabled</summary>
        public UndreamAI.LlamaLib.RemoteLLMAgent pureRemoteAgent { get; protected set; }

        /// <summary>Current conversation history as a list of chat messages</summary>
        public List<ChatMessage> chat
        {
            get
            {
                if (llmAgent == null && pureRemoteAgent == null) return new List<ChatMessage>();

                // convert each UndreamAI.LlamaLib.ChatMessage to LLMUnity.ChatMessage
                return GetHistoryInternal()
                    .Select(m => new ChatMessage(m))
                    .ToList();
            }
            set
            {
                if (llmAgent != null || pureRemoteAgent != null)
                {
                    // convert LLMUnity.ChatMessage back to UndreamAI.LlamaLib.ChatMessage
                    var history = value?.Select(m => (UndreamAI.LlamaLib.ChatMessage)m).ToList()
                        ?? new List<UndreamAI.LlamaLib.ChatMessage>();

                    SetHistoryInternal(history);
                }
            }
        }
        #endregion

        #region Unity Lifecycle and Initialization
        public override void Awake()
        {
            if (!remote) llm?.Register(this);
            base.Awake();
        }

        protected override async Task SetupCallerObject()
        {
            await base.SetupCallerObject();

            string exceptionMessage = "";
            llmAgent = null;
            pureRemoteAgent = null;
            try
            {
                if (llmClient.IsPureCSharpRemote)
                {
                    pureRemoteAgent = new UndreamAI.LlamaLib.RemoteLLMAgent(llmClient.PureRemoteClient, systemPrompt);
                }
                else
                {
                    llmAgent = new UndreamAI.LlamaLib.LLMAgent(llmClient, systemPrompt);
                }
            }
            catch (Exception ex)
            {
                exceptionMessage = ex.Message;
            }
            if ((llmAgent == null && pureRemoteAgent == null) || exceptionMessage != "")
            {
                string error = "LLMAgent not initialized";
                if (exceptionMessage != "") error += ", error: " + exceptionMessage;
                LLMUnitySetup.LogError(error, true);
            }
        }

        /// <summary>
        /// Initialisation after setting up the LLM client (local or remote).
        /// </summary>
        protected override async Task PostSetupCallerObject()
        {
            await base.PostSetupCallerObject();
            if (slot != -1)
            {
                if (llmAgent != null) llmAgent.SlotId = slot;
                if (pureRemoteAgent != null) pureRemoteAgent.SlotId = slot;
            }
            await InitHistory();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            // Validate slot configuration
            if (llm != null && llm.parallelPrompts > -1 && (slot < -1 || slot >= llm.parallelPrompts))
            {
                LLMUnitySetup.LogError($"Slot must be between 0 and {llm.parallelPrompts - 1}, or -1 for auto-assignment");
            }
        }

        protected override LLMLocal GetCaller()
        {
            if (llmAgent != null) return llmAgent;
            if (pureRemoteAgent != null) return llmClient;
            return null;
        }

        /// <summary>
        /// Initializes conversation history by clearing current state and loading from file if available.
        /// </summary>
        protected virtual async Task InitHistory()
        {
            await ClearHistory();
            if (!string.IsNullOrEmpty(save) && File.Exists(GetSavePath()))
            {
                await LoadHistory();
            }
        }

        #endregion

        #region File Path Management
        /// <summary>
        /// Gets the full path for a file in the persistent data directory.
        /// </summary>
        /// <returns>Full file path in persistent data directory</returns>
        public virtual string GetSavePath()
        {
            if (string.IsNullOrEmpty(save))
            {
                LLMUnitySetup.LogError("No save path specified");
                return null;
            }

            return Path.Combine(Application.persistentDataPath, save).Replace('\\', '/');
        }

        #endregion

        #region Chat Management
        /// <summary>
        /// Clears the entire conversation history.
        /// </summary>
        public virtual async Task ClearHistory()
        {
            await CheckCaller(checkConnection: false);
            if (llmAgent != null) llmAgent.ClearHistory();
            else pureRemoteAgent?.ClearHistory();
        }

        /// <summary>
        /// Adds a user message to the conversation history.
        /// </summary>
        /// <param name="content">User message content</param>
        public virtual async Task AddUserMessage(string content)
        {
            await CheckCaller();
            if (llmAgent != null) llmAgent.AddUserMessage(content);
            else pureRemoteAgent?.AddUserMessage(content);
        }

        /// <summary>
        /// Adds structured user content to the conversation history.
        /// For multimodal input, use OpenAI-style content parts in a JSON array.
        /// </summary>
        /// <param name="content">Structured user content (string token or array of parts)</param>
        public virtual async Task AddUserMessageContent(JToken content)
        {
            await CheckCaller();
            if (pureRemoteAgent != null)
            {
                pureRemoteAgent.AddUserMessage(content);
                return;
            }

            if (content == null || content.Type == JTokenType.Null || content.Type == JTokenType.String)
            {
                llmAgent?.AddUserMessage(content?.ToString());
                return;
            }

            throw new NotSupportedException("Structured multimodal user content requires pure C# remote mode (LLMClient.UseNativeForRemote = false).");
        }

        /// <summary>
        /// Adds a multimodal user message (optional text + image URL/data URL) to conversation history.
        /// </summary>
        public virtual async Task AddUserImageMessage(string text, string imageUrlOrDataUrl)
        {
            await AddUserMessageContent(CreateMultimodalContent(text, imageUrlOrDataUrl));
        }

        /// <summary>
        /// Adds a multimodal user message (optional text + image bytes) to conversation history.
        /// </summary>
        public virtual async Task AddUserImageMessageBytes(string text, byte[] imageBytes, string mimeType = "image/png")
        {
            await AddUserMessageContent(CreateMultimodalContent(text, imageBytes, mimeType));
        }

        /// <summary>
        /// Adds a multimodal user message (optional text + image file) to conversation history.
        /// </summary>
        public virtual async Task AddUserImageMessageFile(string text, string imagePath, string mimeType = null)
        {
            await AddUserMessageContent(CreateMultimodalContentFromFile(text, imagePath, mimeType));
        }

        /// <summary>
        /// Adds an AI assistant message to the conversation history.
        /// </summary>
        /// <param name="content">Assistant message content</param>
        public virtual async Task AddAssistantMessage(string content)
        {
            await CheckCaller();
            if (llmAgent != null) llmAgent.AddAssistantMessage(content);
            else pureRemoteAgent?.AddAssistantMessage(content);
        }

        #endregion

        #region Chat Functionality
        /// \cond HIDE
        [Serializable]
        public class CompletionResponseJson
        {
            public string prompt;
            public string content;
        }
        /// \endcond
        /// <summary>
        /// Processes a user query asynchronously and generates an AI response using conversation context.
        /// The query and response are automatically added to chat history if specified.
        /// </summary>
        /// <param name="query">User's message or question</param>
        /// <param name="callback">Optional streaming callback for partial responses</param>
        /// <param name="completionCallback">Optional callback when response is complete</param>
        /// <param name="addToHistory">Whether to add the exchange to conversation history</param>
        /// <returns>Task that returns the AI assistant's response</returns>
        public virtual async Task<string> Chat(string query, Action<string> callback = null,
            Action completionCallback = null, bool addToHistory = true)
        {
            await CheckCaller();
            string result = "";
            try
            {
                LlamaLib.CharArrayCallback wrappedCallback = BuildCallback(callback);
                SetCompletionParameters();
                if (pureRemoteAgent != null)
                {
                    result = await pureRemoteAgent.ChatAsync(query, addToHistory, wrappedCallback, false, debugPrompt);
                }
                else
                {
                    result = await llmAgent.ChatAsync(query, addToHistory, wrappedCallback, false, debugPrompt);
                }
                if (this == null) return null;
                if (addToHistory && result != null && save != "") _ = SaveHistory();
                if (this != null) completionCallback?.Invoke();
            }
            catch (Exception ex)
            {
                LLMUnitySetup.LogError(ex.Message, true);
            }
            return result;
        }

        /// <summary>
        /// Processes structured user content and generates an AI response.
        /// Use this for multimodal requests (text + image) with OpenAI-style content parts.
        /// </summary>
        /// <param name="queryContent">Structured user content token</param>
        /// <param name="callback">Optional streaming callback for partial responses</param>
        /// <param name="completionCallback">Optional callback when response is complete</param>
        /// <param name="addToHistory">Whether to add the exchange to conversation history</param>
        /// <returns>Task that returns the AI assistant's response</returns>
        public virtual async Task<string> ChatContent(JToken queryContent, Action<string> callback = null,
            Action completionCallback = null, bool addToHistory = true)
        {
            await CheckCaller();
            string result = "";
            try
            {
                LlamaLib.CharArrayCallback wrappedCallback = BuildCallback(callback);
                SetCompletionParameters();
                if (pureRemoteAgent != null)
                {
                    result = await pureRemoteAgent.ChatAsync(queryContent, addToHistory, wrappedCallback, false, debugPrompt);
                }
                else if (queryContent == null || queryContent.Type == JTokenType.Null || queryContent.Type == JTokenType.String)
                {
                    result = await llmAgent.ChatAsync(queryContent?.ToString(), addToHistory, wrappedCallback, false, debugPrompt);
                }
                else
                {
                    throw new NotSupportedException("Multimodal chat requires pure C# remote mode (LLMClient.UseNativeForRemote = false).");
                }

                if (this == null) return null;
                if (addToHistory && result != null && save != "") _ = SaveHistory();
                if (this != null) completionCallback?.Invoke();
            }
            catch (Exception ex)
            {
                LLMUnitySetup.LogError(ex.Message, true);
            }
            return result;
        }

        /// <summary>
        /// Creates a multimodal user content payload with optional text and an image URL/data URL.
        /// </summary>
        public static JArray CreateMultimodalContent(string text, string imageUrlOrDataUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrlOrDataUrl))
                throw new ArgumentNullException(nameof(imageUrlOrDataUrl));

            var parts = new JArray();
            if (!string.IsNullOrEmpty(text))
            {
                parts.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = text
                });
            }

            parts.Add(new JObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JObject
                {
                    ["url"] = imageUrlOrDataUrl
                }
            });
            return parts;
        }

        /// <summary>
        /// Creates a multimodal user content payload with optional text and image bytes.
        /// </summary>
        public static JArray CreateMultimodalContent(string text, byte[] imageBytes, string mimeType = "image/png")
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentNullException(nameof(imageBytes));
            if (string.IsNullOrWhiteSpace(mimeType))
                mimeType = "image/png";

            string base64 = Convert.ToBase64String(imageBytes);
            string dataUrl = $"data:{mimeType};base64,{base64}";
            return CreateMultimodalContent(text, dataUrl);
        }

        /// <summary>
        /// Creates a multimodal user content payload from an image file path.
        /// </summary>
        public static JArray CreateMultimodalContentFromFile(string text, string imagePath, string mimeType = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentNullException(nameof(imagePath));
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}", imagePath);

            string resolvedMimeType = string.IsNullOrWhiteSpace(mimeType) ? GuessMimeTypeFromPath(imagePath) : mimeType;
            byte[] bytes = File.ReadAllBytes(imagePath);
            return CreateMultimodalContent(text, bytes, resolvedMimeType);
        }

        /// <summary>
        /// Sends a multimodal chat request with optional text and image URL/data URL.
        /// </summary>
        public virtual async Task<string> ChatWithImage(string text, string imageUrlOrDataUrl, Action<string> callback = null,
            Action completionCallback = null, bool addToHistory = true)
        {
            return await ChatContent(CreateMultimodalContent(text, imageUrlOrDataUrl), callback, completionCallback, addToHistory);
        }

        /// <summary>
        /// Sends a multimodal chat request with optional text and image bytes.
        /// </summary>
        public virtual async Task<string> ChatWithImageBytes(string text, byte[] imageBytes, string mimeType = "image/png",
            Action<string> callback = null, Action completionCallback = null, bool addToHistory = true)
        {
            return await ChatContent(CreateMultimodalContent(text, imageBytes, mimeType), callback, completionCallback, addToHistory);
        }

        /// <summary>
        /// Sends a multimodal chat request with optional text and image file.
        /// </summary>
        public virtual async Task<string> ChatWithImageFile(string text, string imagePath, string mimeType = null,
            Action<string> callback = null, Action completionCallback = null, bool addToHistory = true)
        {
            return await ChatContent(CreateMultimodalContentFromFile(text, imagePath, mimeType), callback, completionCallback, addToHistory);
        }

        /// <summary>
        /// Warms up the model by processing the system prompt without generating output.
        /// This caches the system prompt processing for faster subsequent responses.
        /// </summary>
        /// <param name="completionCallback">Optional callback when warmup completes</param>
        /// <returns>Task that completes when warmup finishes</returns>
        public virtual async Task Warmup(Action completionCallback = null)
        {
            await Warmup(null, completionCallback);
        }

        /// <summary>
        /// Warms up the model with a specific prompt without adding it to history.
        /// This pre-processes prompts for faster response times in subsequent interactions.
        /// </summary>
        /// <param name="query">Warmup prompt (not added to history)</param>
        /// <param name="completionCallback">Optional callback when warmup completes</param>
        /// <returns>Task that completes when warmup finishes</returns>
        public virtual async Task Warmup(string query, Action completionCallback = null)
        {
            int originalNumPredict = numPredict;
            try
            {
                // Set to generate no tokens for warmup
                numPredict = 0;
                await Chat(query, null, completionCallback, false);
            }
            finally
            {
                // Restore original setting
                numPredict = originalNumPredict;
                SetCompletionParameters();
            }
        }

        #endregion

        #region Persistence
        /// <summary>
        /// Saves the conversation history and optionally the LLM cache to disk.
        /// </summary>
        public virtual async Task SaveHistory()
        {
            if (string.IsNullOrEmpty(save))
            {
                LLMUnitySetup.LogError("No save path specified");
                return;
            }
            await CheckCaller();

            // Save chat history
            string jsonPath = GetSavePath();
            string directory = Path.GetDirectoryName(jsonPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                if (llmAgent != null) llmAgent.SaveHistory(jsonPath);
                else pureRemoteAgent?.SaveHistory(jsonPath);
                LLMUnitySetup.Log($"Saved chat history to: {jsonPath}");
            }
            catch (Exception ex)
            {
                LLMUnitySetup.LogError($"Failed to save chat history to '{jsonPath}': {ex.Message}", true);
            }
        }

        /// <summary>
        /// Loads conversation history and optionally the LLM cache from disk.
        /// </summary>
        public virtual async Task LoadHistory()
        {
            if (string.IsNullOrEmpty(save))
            {
                LLMUnitySetup.LogError("No save path specified");
                return;
            }
            await CheckCaller();

            // Load chat history
            string jsonPath = GetSavePath();
            if (!File.Exists(jsonPath))
            {
                LLMUnitySetup.LogError($"Chat history file not found: {jsonPath}");
            }

            try
            {
                if (llmAgent != null) llmAgent.LoadHistory(jsonPath);
                else pureRemoteAgent?.LoadHistory(jsonPath);
                LLMUnitySetup.Log($"Loaded chat history from: {jsonPath}");
            }
            catch (Exception ex)
            {
                LLMUnitySetup.LogError($"Failed to load chat history from '{jsonPath}': {ex.Message}", true);
            }
        }

        #endregion

        #region Request Management
        /// <summary>
        /// Cancels any active requests for this agent.
        /// </summary>
        public void CancelRequests()
        {
            if (llmAgent != null) llmAgent.Cancel();
            else pureRemoteAgent?.Cancel(slot);
        }

        #endregion

        #region Internals
        protected virtual List<UndreamAI.LlamaLib.ChatMessage> GetHistoryInternal()
        {
            if (llmAgent != null) return llmAgent.GetHistory();
            if (pureRemoteAgent != null) return pureRemoteAgent.GetHistory();
            return new List<UndreamAI.LlamaLib.ChatMessage>();
        }

        protected virtual void SetHistoryInternal(List<UndreamAI.LlamaLib.ChatMessage> history)
        {
            if (llmAgent != null) llmAgent.SetHistory(history);
            else pureRemoteAgent?.SetHistory(history);
        }

        private LlamaLib.CharArrayCallback BuildCallback(Action<string> callback)
        {
            if (callback == null) return null;
#if ENABLE_IL2CPP
            // For IL2CPP: wrap to IntPtr callback, then wrap for main thread
            Action<string> mainThreadCallback = Utils.WrapActionForMainThread(callback, this);
            return IL2CPP_Completion.CreateCallback(mainThreadCallback);
#else
            // For Mono: direct callback wrapping
            return Utils.WrapCallbackForAsync(callback, this);
#endif
        }

        private static string GuessMimeTypeFromPath(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                _ => "application/octet-stream"
            };
        }
        #endregion
    }

    public class ChatMessage : UndreamAI.LlamaLib.ChatMessage
    {
        public ChatMessage(string role, string content) : base(role, content) {}
        public ChatMessage(string role, Newtonsoft.Json.Linq.JToken content)
            : base(role, content?.ToString(Newtonsoft.Json.Formatting.None) ?? string.Empty)
        {
            TrySetStructuredContent(this, content);
        }
        public ChatMessage(UndreamAI.LlamaLib.ChatMessage other) : base(other.role, other.content) {}

        private static void TrySetStructuredContent(UndreamAI.LlamaLib.ChatMessage message, Newtonsoft.Json.Linq.JToken content)
        {
            // Some builds expose content as JToken; older ones keep string-only payloads.
            var prop = message.GetType().GetProperty("content");
            if (prop == null || !prop.CanWrite) return;
            if (!typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(prop.PropertyType)) return;
            prop.SetValue(message, content ?? JValue.CreateNull(), null);
        }
    }
}
