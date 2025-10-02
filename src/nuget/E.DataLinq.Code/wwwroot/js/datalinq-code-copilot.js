let controllerTargetUrl;
let dlToken;
let messages;
let currentChatId;
let chatDatabase;
let currentUser;
let chatHistories;

async function CopilotInitializer() {
    if (controllerTargetUrl === undefined) {
        controllerTargetUrl = window.location.origin;

        const theme = sessionStorage.getItem('editorTheme');

        document.body.classList.toggle('colorscheme-light', theme === 'vs');

        window.addEventListener('message', function (event) {
            const data = event.data;
            if (data && typeof data.theme === 'string') {
                document.body.classList.toggle('colorscheme-light', data.theme === 'vs');
            }
        });

        const urlParams = new URLSearchParams(window.location.search);
        dlToken = urlParams.get("dl_token") || null;

        chatDatabase = await initChatDatabase();

        messages = [];
        currentChatId = sessionStorage.getItem('currentChatId') || "";
        currentUser = parent.document.querySelector("#toolbar > div.datalinq-code-toolbutton.logout > div:nth-child(1)")?.textContent?.trim();

        if (currentChatId) {
            loadChatHistory(currentChatId);
        }
    }
}

    async function initChatDatabase() {
    if (chatDatabase) {
        return chatDatabase;
    }

    return new Promise((resolve, reject) => {
        const request = indexedDB.open('DataLinqCopilotChats', 1);

        request.onerror = () => {
            console.error('Error opening database:', request.error);
            reject(request.error);
        };

        request.onsuccess = async () => {
            console.log('Database opened successfully');
            chatDatabase = request.result; 

            try {
                await loadChatHistories();
                console.log('Chat histories loaded:', chatHistories.length, 'chats');
            } catch (error) {
                console.error('Error loading chat histories:', error);
            }

            resolve(chatDatabase);
        };

        request.onupgradeneeded = (event) => {
            const db = event.target.result;

            const store = db.createObjectStore('chatHistories', { keyPath: 'id' });

            store.createIndex('userId', 'userId', { unique: false });
            store.createIndex('createdAt', 'createdAt', { unique: false });
            store.createIndex('updatedAt', 'updatedAt', { unique: false });
            store.createIndex('title', 'title', { unique: false });

            console.log('Database schema created successfully');
        };


    });
}

CopilotInitializer();

    async function sendMessageAsync(inputMessage, command) {
        if (!currentChatId) {
            currentChatId = crypto.randomUUID();
            sessionStorage.setItem('currentChatId', currentChatId);
        }

        try {
            let message = getMessageInput(inputMessage);

            if (!message) {
                return;
            }

            hideIntroSection();
            showTopHint();

            const displayText = command || message;
            createChatMessage(displayText, true);

            createChatMessage('<img src="/_content/E.DataLinq.Code/css/img/copilot-spinner@1.gif" style="height: 25px;"/>', false);

            message = preprocessMessage(message);

            messages.push(message);

            await saveChatToIndexedDB(currentChatId, messages);

            const answer = await askCopilot(messages);

            deleteLastChatMessage();
            createChatMessage(answer, false);
            messages.push(answer);

            await saveChatToIndexedDB(currentChatId, messages);

            scrollChatToBottom();

        } catch (err) {
            console.error("Error in sendMessageAsync:", err);

            createChatMessage("Sorry, there was an error processing your request. Please try again.", false);
            scrollChatToBottom();
        }
    }

    function deleteLastChatMessage() {
        const history = document.getElementById("copilot-chat-history");
        if (history.lastChild) {
            history.removeChild(history.lastChild);
        }
}

document.addEventListener('DOMContentLoaded', () => {
    const i = document.getElementById('copilot-inputfield');
    const b = document.getElementById('copilot-submit');
    i?.addEventListener('keydown', e => e.key === 'Enter' && !e.shiftKey && !e.isComposing && (e.preventDefault(), b?.click()));
});

document.addEventListener('DOMContentLoaded', () => {
    const ta = document.getElementById('copilot-inputfield');
    const btn = document.getElementById('copilot-submit');
    if (!ta || !btn) return;

    const maxLines = 4;

    // Pre-calculate single-line height
    const style = window.getComputedStyle(ta);
    const lineHeight = parseFloat(style.lineHeight) || 20;
    const verticalPadding =
        parseFloat(style.paddingTop) +
        parseFloat(style.paddingBottom) +
        parseFloat(style.borderTopWidth) +
        parseFloat(style.borderBottomWidth);

    function autoResize() {
        ta.classList.remove('scrollable');
        ta.style.height = 'auto';
        const fullHeight = ta.scrollHeight;
        const maxHeight = lineHeight * maxLines + verticalPadding;

        if (fullHeight <= maxHeight) {
            ta.style.height = fullHeight + 'px';
            ta.style.overflowY = 'hidden';
        } else {
            ta.style.height = maxHeight + 'px';
            ta.style.overflowY = 'auto';
            ta.classList.add('scrollable');
        }
    }

    // Initial sizing
    autoResize();

    ta.addEventListener('input', autoResize);

    ta.addEventListener('keydown', e => {
        if (e.isComposing) return;

        // Plain Enter submits
        if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.altKey && !e.metaKey) {
            e.preventDefault();
            btn.click();
            return;
        }

        // Shift+Enter => allow newline; resize happens on input event
    });
});

    async function toggleChatHistory() {
        try {
            await loadChatHistories();
            $('body').dataLinq_code_modal({
                title: 'Chat Verlauf auswählen...',
                onload: function ($content) {
                    renderChatHistoryList(null, $content, chatHistories);
                }
            });
        } catch (error) {
            console.error('Error opening chat history modal:', error);
            alert('Fehler beim Laden des Chat-Verlaufs');
        }
    }

    async function startNewChat()
    {
        deleteHistory();
    }

    function toggleExpand()
    {
        window.parent.postMessage({ action: 'expand-copilot' }, '*');
    }

    function deleteHistory() {
        const hint = document.querySelector(".top-hint");
        hint.style.display = "none";

        const intro = document.getElementById("copilot-intro");
        intro.classList.remove("hidden");

        const history = document.getElementById("copilot-chat-history");
        history.innerHTML = "";

        messages = [];
        currentChatId = "";
        sessionStorage.removeItem('currentChatId');
    }


async function saveChatToIndexedDB(chatId, messages) {
    try {
        if (!chatDatabase) {
            await initChatDatabase();
        }

        const transaction = chatDatabase.transaction(['chatHistories'], 'readwrite');
        const store = transaction.objectStore('chatHistories');

        const getRequest = store.get(chatId);

        return new Promise((resolve, reject) => {
            getRequest.onsuccess = () => {
                const existingChat = getRequest.result;
                const now = new Date().toISOString();

                let chatData;
                if (existingChat) {
                    chatData = {
                        ...existingChat,
                        messages: [...messages],
                        updatedAt: now
                    };

                    if ((!existingChat.title || existingChat.title === 'Neuer Chat') && messages.length > 0) {
                        chatData.title = generateChatTitle(messages[0]);
                    }
                } else {
                    chatData = {
                        id: chatId,
                        title: messages.length > 0 ? generateChatTitle(messages[0]) : 'Neuer Chat',
                        messages: [...messages],
                        createdAt: now,
                        updatedAt: now,
                        userId: currentUser
                    };
                }

                const putRequest = store.put(chatData);
                putRequest.onsuccess = () => {
                    console.log('Chat saved to IndexedDB:', chatId);
                    resolve(chatData);
                };
                putRequest.onerror = () => {
                    console.error('Error saving chat:', putRequest.error);
                    reject(putRequest.error);
                };
            };

            getRequest.onerror = () => {
                console.error('Error getting chat:', getRequest.error);
                reject(getRequest.error);
            };
        });

    } catch (error) {
        console.error('Error in saveChatToIndexedDB:', error);
        throw error;
    }
}

async function loadChatHistories() {
    return new Promise((resolve, reject) => {
        const transaction = chatDatabase.transaction(['chatHistories'], 'readonly');
        const store = transaction.objectStore('chatHistories');
        const userIndex = store.index('userId');

        const request = userIndex.getAll(currentUser);

        request.onsuccess = () => {
            chatHistories = request.result || [];

            chatHistories.sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt));

            console.log(`Loaded chat histories for ${currentUser}:`, chatHistories.length);
            resolve(chatHistories);
        };

        request.onerror = () => {
            console.error('Error loading chat histories:', request.error);
            chatHistories = []; // Set empty array on error
            reject(request.error);
        };
    });
}

    function generateChatTitle(firstMessage) {
        if (!firstMessage || typeof firstMessage !== 'string') {
            return `Chat ${new Date().toLocaleDateString('de-DE')} ${new Date().toLocaleTimeString('de-DE')}`;
        }

        const title = firstMessage.substring(0, 50);
        return title + (firstMessage.length > 50 ? '...' : '');
    }

    var renderChatHistoryList = function ($parent, $content, chatHistories) {
        let $ul = $("<ul>")
            .addClass('datalinq-code-app-prefixes')
            .appendTo($content);

        if (!chatHistories || chatHistories.length === 0) {
            $("<li>")
                .addClass('datalinq-code-app-prefix')
                .appendTo($ul)
                .append(
                    $("<div>")
                        .addClass('text')
                        .text('Keine Chat-Verläufe vorhanden')
                );
            return;
        }

        $.each(chatHistories, function (index, chat) {
            var $li = $("<li>")
                .addClass('datalinq-code-app-prefix')
                .data('chat-id', chat.id)
                .appendTo($ul)
                .click(function (e) {
                    e.stopPropagation();
                    loadChatHistory(chat.id);
                    $(null).dataLinq_code_modal('close');
                });

            $("<div>")
                .addClass('text')
                .text(chat.title)
                .appendTo($li);

            var dateStr = new Date(chat.updatedAt).toLocaleDateString('de-DE');
            var messageCount = chat.messages ? chat.messages.length : 0;
            var subtext = `${dateStr} • ${messageCount} Nachrichten`;

            $("<div>")
                .addClass('subtext')
                .text(subtext)
                .appendTo($li);

            $("<div>")
                .addClass('checkbox')
                .text('×')
                .css('cursor', 'pointer')
                .appendTo($li)
                .click(function (e) {
                    e.stopPropagation();
                    dataLinqConfirm(
                        `Chat "${chat.title}" wirklich löschen?`,
                        'Chat löschen',
                        function () {
                            deleteChatHistory(chat.id);
                            $li.remove();
                        },
                        function () {
                            console.log('Chat deletion cancelled');
                        }
                    );
                });
        });

        let $buttons = $("<div>")
            .addClass('datalinq-code-buttons-bar-right')
            .appendTo($content);

        $("<button>")
            .addClass('datalinq-code-button')
            .text('Neuer Chat')
            .appendTo($buttons)
            .click(function () {
                $(null).dataLinq_code_modal('close');
                startNewChat();
            });

        $("<button>")
            .addClass('datalinq-code-button cancel')
            .text('Alle löschen')
            .appendTo($buttons)
            .click(function () {
                dataLinqConfirm(
                    'Wirklich alle Chat-Verläufe löschen? Diese Aktion kann nicht rückgängig gemacht werden.',
                    'Alle Chats löschen',
                    function () {
                        $.each(chatHistories, function (index, chat) {
                            deleteChatHistory(chat.id);
                        });
                        $(null).dataLinq_code_modal('close');
                        startNewChat();
                    }
                );
            });

        $("<button>")
            .addClass('datalinq-code-button')
            .text('Schließen')
            .appendTo($buttons)
            .click(function () {
                $(null).dataLinq_code_modal('close');
            });
    };
    function dataLinqConfirm(message, title, onConfirm, onCancel) {
        title = title || 'Bestätigung';

        $('body').dataLinq_code_modal({
            title: title,
            height: '200px',
            width: '640px',
            id: 'datalinq-code-confirm',
            onload: function ($content) {
                $("<p>")
                    .text(message)
                    .appendTo($content.addClass('datalinq-code-messagebox-content'));

                var $buttonbar = $("<div>").addClass("button-bar").appendTo($content);

                $("<button>")
                    .addClass("datalinq-code-button cancel")
                    .text("Nein")
                    .appendTo($buttonbar)
                    .click(function () {
                        if (onCancel) {
                            onCancel();
                        }
                        $('body').dataLinq_code_modal('close', { id: 'datalinq-code-confirm' });
                    });

                $("<button>")
                    .addClass("datalinq-code-button")
                    .text("Ja")
                    .appendTo($buttonbar)
                    .click(function () {
                        if (onConfirm) {
                            onConfirm();
                        }
                        $('body').dataLinq_code_modal('close', { id: 'datalinq-code-confirm' });
                    });
            }
        });
    }


    async function deleteChatHistory(id) {
        return new Promise((resolve, reject) => {
            const transaction = chatDatabase.transaction(['chatHistories'], 'readwrite');
            const store = transaction.objectStore('chatHistories');

            const request = store.delete(id);

            request.onsuccess = () => {
                console.log(`History deleted: ${id}`);

                if (currentChatId == id) {
                    deleteHistory();
                }
            };

            request.onerror = () => {
                console.error('Error deleting history:', request.error);
                reject(request.error);
            };
        });
    }

async function loadChatHistory(id) {
    return new Promise((resolve, reject) => {
        const transaction = chatDatabase.transaction(['chatHistories'], 'readonly');
        const store = transaction.objectStore('chatHistories');

        const request = store.get(id);

        request.onsuccess = () => {
            const chatData = request.result;

            if (chatData) {
                deleteHistory();

                currentChatId = chatData.id;
                sessionStorage.setItem('currentChatId', currentChatId);
                messages = [...chatData.messages]; 

                console.log(`Loaded chat history with id: ${id}`, chatData);
                console.log(`Messages loaded: ${messages.length}`);

                displayLoadedMessages();
                hideIntroSection();
                showTopHint();

                resolve(chatData);
            } else {
                console.error(`Chat with id ${id} not found`);
                reject(new Error(`Chat with id ${id} not found`));
            }
        };

        request.onerror = () => {
            console.error('Error loading chat:', request.error);
            reject(request.error);
        };
    });
}

function displayLoadedMessages() {
    messages.forEach((message, index) => {
        const isUserMessage = index % 2 === 0;
        createChatMessage(message, isUserMessage);
    });
    scrollChatToBottom();
}

    async function loadHistory() {

    }

    function preprocessMessage(message) {
        if (message.includes('#current')) {
            const currentCode = getCurrentCode();
            return message.replace(/#current/g, currentCode);
        }
        return message;
    }

    function addCopyButtonsToCodeBlocks(element) {
        element.querySelectorAll("pre").forEach(pre => {
            const button = createCopyButton();
            const wrapper = createCodeWrapper(pre, button);

            pre.replaceWith(wrapper);

            button.addEventListener("click", () => handleCopyClick(button, wrapper));
        });
    }

    function createCopyButton() {
        const button = document.createElement("button");
        button.textContent = "Copy";
        button.className = "copy-btn";

        Object.assign(button.style, {
            position: "absolute",
            top: "8px",
            right: "8px",
            padding: "2px 6px",
            fontSize: "0.75rem",
            cursor: "pointer"
        });

        return button;
    }

    function createCodeWrapper(pre, button) {
        const wrapper = document.createElement("div");
        wrapper.style.position = "relative";
        wrapper.appendChild(pre.cloneNode(true));
        wrapper.appendChild(button);
        return wrapper;
    }

    function handleCopyClick(button, wrapper) {
        const codeText = wrapper.querySelector("pre").innerText;
        navigator.clipboard.writeText(codeText).then(() => {
            button.textContent = "Copied!";
            setTimeout(() => (button.textContent = "Copy"), 1500);
        }).catch(err => {
            console.error("Failed to copy text:", err);
            button.textContent = "Error";
            setTimeout(() => (button.textContent = "Copy"), 1500);
        });
    }

    function createChatMessage(content, isUser = false, isCommand = false) {
        const history = document.getElementById("copilot-chat-history");
        const messageDiv = document.createElement("div");

        messageDiv.classList.add("chat-message");
        messageDiv.classList.add(isUser ? "user-message" : "assistant-message");

        if (isUser) {
            messageDiv.textContent = content;
        } else {
            messageDiv.innerHTML = marked.parse(content);
            addCopyButtonsToCodeBlocks(messageDiv);
        }

        history.appendChild(messageDiv);
        return messageDiv;
    }

function getMessageInput(inputMessage) {
    if (inputMessage) return inputMessage;

    const input = document.getElementById('copilot-inputfield');
    if (!input) {
        console.warn('getMessageInput: #copilot-inputfield not found');
        return '';
    }

    const message = input.value.trim();
    input.value = '';
    return message;
}

    function hideIntroSection() {
        const intro = document.getElementById("copilot-intro");
        if (intro) {
            intro.classList.add("hidden");
        }
    }

    function showTopHint() {
        const hint = document.querySelector(".top-hint");
        if (hint) {
            hint.style.display = "block";
        }
}

function scrollChatToBottom() {
    document.body.scrollTop = document.body.scrollHeight;
}

    async function askCopilot(questions) {
        if (!controllerTargetUrl || !dlToken) {
            throw new Error("Controller URL or dl_token is not initialized!");
        }

        if (!questions || !Array.isArray(questions) || questions.length === 0) {
            throw new Error("Questions must be a non-empty array");
        }

        const url = `${controllerTargetUrl}/datalinqcode/AskDatalinqCopilot?dl_token=${dlToken}`;

        try {
            const formData = new FormData();
            questions.forEach(question => {
                if (question && question.trim()) {
                    formData.append("questions", question.trim());
                }
            });

            const response = await fetch(url, {
                method: "POST",
                body: formData,
                headers: {}
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`API request failed: ${response.status} ${response.statusText}. ${errorText}`);
            }

            const data = await response.json();

            if (!data || typeof data.answer === 'undefined') {
                throw new Error("Invalid response format: missing answer field");
            }

            return data.answer;

        } catch (error) {
            console.error("Error calling Copilot API:", {
                url,
                questionsCount: questions.length,
                error: error.message
            });

            throw new Error(`Failed to get response from Copilot: ${error.message}`);
        }
    }

    async function explainCurrentCode() {
        try {
            const code = getCurrentCode();

            if (!code || code.trim() === '') {
                await sendMessageAsync(
                    "Es ist kein Code in der aktuellen View verfügbar zum Erklären.",
                    "Erkläre mir den Code des aktuellen Tab"
                );
                return;
            }

            const message = `Erkläre mir den Code der aktuellen View:\n\n${code}`;

            await sendMessageAsync(message, "Erkläre mir den Code des aktuellen Tab");

        } catch (error) {
            console.error("Error in explainCurrentCode function:", error);
            await sendMessageAsync(
                `Es gab einen Fehler beim Abrufen des aktuellen Codes: ${error.message}`,
                "Fehler beim Abrufen des Codes"
            );
        }
    }

    async function currentError() {
        try {
            const code = getCurrentCode();
            const errors = await getCurrentErrors();

            if (!code) {
                await sendMessageAsync(
                    "Es ist kein Code in der aktuellen View verfügbar.",
                    "Erkläre mir was der aktuelle Fehler bedeutet"
                );
                return;
            }

            if (errors.length === 0) {
                await sendMessageAsync(
                    `In der aktuellen View:\n${code}\n\nEs wurden keine Compiler-Fehler gefunden.`,
                    "Erkläre mir den Code des aktuellen Tab"
                );
                return;
            }

            const message = `Warum bekomme ich in der aktuellen View:\n${code}\n\ndie folgenden Errors:\n${errors.join('\n')}`;

            await sendMessageAsync(message, "Erkläre mir was der aktuelle Fehler bedeutet");

        } catch (error) {
            console.error("Error in currentError function:", error);
            await sendMessageAsync(
                `Es gab einen Fehler beim Abrufen der aktuellen Fehler: ${error.message}`,
                "Fehler beim Abrufen der Daten"
            );
        }
    }

    async function submitFormAndGetResult(targetIframe) {
        if (!targetIframe) {
            throw new Error("Target iframe is required");
        }

        const doc = targetIframe.contentWindow.document;
        const form = doc.querySelector("form");

        if (!form) {
            throw new Error("No form found in the target iframe");
        }

        if (!dlToken) {
            throw new Error("dlToken is not available");
        }

        const formData = new FormData(form);
        const action = form.action;
        const method = form.method || "POST";

        try {
            const response = await fetch(action, {
                method: method,
                body: formData,
                headers: {
                    "Authorization": `Bearer ${dlToken}`
                }
            });

            if (!response.ok) {
                throw new Error(`Request failed with status: ${response.status} ${response.statusText}`);
            }

            try {
                return await response.json();
            } catch {
                return await response.text();
            }

        } catch (error) {
            console.error("Error submitting form:", error);
            throw error;
        }
    }

    function getCurrentCode() {
        const targetIframe = getCurrentTabIframe();
        if (!targetIframe) {
            return '';
        }

        try {
            const iframeDoc = targetIframe.contentWindow.document;
            const hiddenInput = iframeDoc.querySelector('input[name="Code"]');

            if (!hiddenInput) {
                console.warn("Code input field not found");
                return '';
            }

            return hiddenInput.value || '';
        } catch (error) {
            console.error("Error accessing iframe content:", error);
            return '';
        }
    }

    async function getCurrentErrors() {
        const targetIframe = getCurrentTabIframe();
        if (!targetIframe) {
            return [];
        }

        try {
            const result = await submitFormAndGetResult(targetIframe);

            if (!result || result.success) {
                return [];
            }

            if (!result.compiler_errors || !Array.isArray(result.compiler_errors)) {
                return [];
            }

            return result.compiler_errors
                .filter(error => error && (error.code_line || error.error_text))
                .map(error => `code_line:${error.code_line || 'unknown'};error_text:${error.error_text || 'no description'}`);

        } catch (error) {
            console.error("Error getting current errors:", error);
            return [`Error retrieving compilation errors: ${error.message}`];
        }
    }

    function getCurrentTabIframe() {
        const selectedTab = getCurrentSelectedTab();
        if (!selectedTab) {
            console.warn("No selected tab found");
            return null;
        }

        const targetIframe = window.parent.document.querySelector(
            `.datalinq-code-editor-frame[data-id="${selectedTab.dataset.id}"]`
        );

        if (!targetIframe) {
            console.warn(`No iframe found for tab ID: ${selectedTab.dataset.id}`);
        }

        return targetIframe;
    }

    function getCurrentSelectedTab() {
        const tabs = window.parent.document.querySelector(".datalinq-code-tabs");
        if (!tabs) {
            console.warn("No tabs container found");
            return null;
        }

        return tabs.querySelector(".datalinq-code-tab.selected");
}