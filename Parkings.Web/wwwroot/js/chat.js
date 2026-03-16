window.chatClient = {
    connection: null,
    dotnetRef: null,

    async start(hubUrl, token, dotnetRef) {
        this.dotnetRef = dotnetRef;

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { accessTokenFactory: () => token })
            .withAutomaticReconnect()
            .build();

        this.connection.on('ReceiveMessage', (msg) => {
            dotnetRef.invokeMethodAsync('OnReceiveMessage', msg);
        });
        this.connection.on('MessageSent', (msg) => {
            dotnetRef.invokeMethodAsync('OnMessageSent', msg);
        });
        this.connection.on('MessagesRead', (data) => {
            dotnetRef.invokeMethodAsync('OnMessagesRead', data.parkingId, data.readBy);
        });

        try { await this.connection.start(); }
        catch (e) { console.error('SignalR error', e); }
    },

    async sendMessage(receiverId, parkingId, text) {
        if (this.connection?.state === 'Connected')
            await this.connection.invoke('SendMessage', receiverId, parkingId, text);
    },

    async markRead(otherUserId, parkingId) {
        if (this.connection?.state === 'Connected')
            await this.connection.invoke('MarkRead', otherUserId, parkingId);
    },

    async stop() {
        if (this.connection) await this.connection.stop();
    },

    scrollToBottom(id) {
        setTimeout(() => {
            const el = document.getElementById(id);
            if (el) el.scrollTop = el.scrollHeight;
        }, 30);
    },

    setRef(ref) {
        window._chatWidgetRef = ref;
    },
};

// В конце chat.js
window.openChatWith = function (ownerId, ownerName, parkingId, parkingName) {
    // Ищем DotNet ref виджета и вызываем метод
    if (window._chatWidgetRef) {
        window._chatWidgetRef.invokeMethodAsync(
            'StartDialogWith', ownerId, ownerName, parkingId, parkingName);
    }
};
