function getDlhCompletionProvider(monaco) {
    return {
        provideCompletionItems: function (model, position) {
            console.log('providerCompletionItems', model, position);
            return [];
        }
    };
}