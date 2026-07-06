/*
 * Browser compatibility notice for PDF report mode.
 *
 * Warns that the PDF may not render correctly in unsupported browsers and
 * remembers the user's acceptance in localStorage.
 */

// Wait until the DataLinq core client-side logic has finished before wiring up the notice.
dataLinq.events.on('onpageloaded', function () {
    const modal = document.getElementById('datalinq-modal');
    const acceptBtn = document.getElementById('accept-btn');
    const declineBtn = document.getElementById('decline-btn');

    if (!modal || !acceptBtn || !declineBtn) {
        return;
    }

    document.body.classList.add('modal-open');

    // Hides the modal and restores page scrolling.
    function closeModal() {
        modal.classList.add('hidden');
        document.body.classList.remove('modal-open');
    }

    acceptBtn.addEventListener('click', function () {
        closeModal();
        localStorage.setItem('datalinq-browser-accepted', 'true');
    });

    declineBtn.addEventListener('click', function () {
        if (confirm('Are you sure you want to decline? The PDF may not render correctly in unsupported browsers.')) {
            closeModal();
        }
    });
});