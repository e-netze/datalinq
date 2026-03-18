dataLinq.events.on('onpageloaded', function () {
        const modal = document.getElementById('datalinq-modal');
        const acceptBtn = document.getElementById('accept-btn');
        const declineBtn = document.getElementById('decline-btn');

        document.body.classList.add('modal-open');

        acceptBtn.addEventListener('click', function () {
            modal.classList.add('hidden');
            localStorage.setItem('datalinq-browser-accepted', 'true');
        });

        declineBtn.addEventListener('click', function () {
            if (confirm('Are you sure you want to decline?  The PDF may not render correctly in unsupported browsers.')) {
                modal.classList.add('hidden');
            }
        });
});