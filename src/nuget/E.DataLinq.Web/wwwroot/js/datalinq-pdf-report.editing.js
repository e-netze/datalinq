/* WAIT FOR DATALINQ CORE CLIENT SIDE LOGIC TO FINISH */
dataLinq.events.on('onpageloaded', function () {

    /* @DLH.NewPdfElement DRAG AND DROP, SNAPPING LOGIC*/
    const elements = document.querySelectorAll('.element');

    elements.forEach(element => {
        let isDragging = false;
        let startX, startY;
        let initialX = 0, initialY = 0;

        const page = element.closest('.page');
        const snapThreshold = 15;

        let snapLineX = document.createElement('div');
        snapLineX.className = 'snap-guide-line-x';
        page.appendChild(snapLineX);

        let snapLineY = document.createElement('div');
        snapLineY.className = 'snap-guide-line-y';
        page.appendChild(snapLineY);

        element.addEventListener('mousedown', startDrag);

        function startDrag(e) {
            isDragging = true;
            element.classList.add('dragging');

            const style = window.getComputedStyle(element);
            const matrix = new DOMMatrix(style.transform);
            initialX = matrix.m41;
            initialY = matrix.m42;

            startX = e.clientX - initialX;
            startY = e.clientY - initialY;

            document.addEventListener('mousemove', drag);
            document.addEventListener('mouseup', stopDrag);

            e.preventDefault();
        }

        function drag(e) {
            if (!isDragging) return;

            let newX = e.clientX - startX;
            let newY = e.clientY - startY;

            const pageRect = page.getBoundingClientRect();
            const elementRect = element.getBoundingClientRect();

            const maxX = pageRect.width - elementRect.width;

            newX = Math.max(0, Math.min(newX, maxX));
            newY = Math.max(0, newY); // Only constrain the top (minimum), allow going below

            let isSnapped = false;
            let snapInfo = null;

            const verticalLine = page.querySelector('.vertical-middle-line');
            const horizontalLine = page.querySelector('.horizontal-middle-line');
            const verticalLineVisible = verticalLine && window.getComputedStyle(verticalLine).display === 'block';
            const horizontalLineVisible = horizontalLine && window.getComputedStyle(horizontalLine).display === 'block';
            const guideLinesVisible = verticalLineVisible || horizontalLineVisible;

            if (e.ctrlKey && guideLinesVisible) {
                const snapped = applySnapping(newX, newY, elementRect, pageRect, verticalLineVisible, horizontalLineVisible);
                newX = snapped.x;
                newY = snapped.y;
                isSnapped = snapped.snapped;

                hideSnapGuides();
            } else if (e.ctrlKey) {
                const snapped = applyElementSnapping(newX, newY, element, page);
                newX = snapped.x;
                newY = snapped.y;
                isSnapped = snapped.snapped;
                snapInfo = snapped.snapInfo;

                if (isSnapped && snapInfo) {
                    showSnapGuides(snapInfo);
                } else {
                    hideSnapGuides();
                }
            } else {
                hideSnapGuides();
            }

            if (isSnapped) {
                element.classList.add('element-snapped');
            } else {
                element.classList.remove('element-snapped');
            }

            element.style.transform = `translate(${newX}px, ${newY}px)`;

            element.setAttribute('data-x', newX);
            element.setAttribute('data-y', newY);
        }

        function applySnapping(x, y, elementRect, pageRect, snapToVerticalLine, snapToHorizontalLine) {
            const elementWidth = elementRect.width;
            const elementHeight = elementRect.height;

            const elementLeft = x;
            const elementRight = x + elementWidth;
            const elementCenterX = x + elementWidth / 2;

            const elementTop = y;
            const elementBottom = y + elementHeight;
            const elementCenterY = y + elementHeight / 2;

            const pageCenterX = pageRect.width / 2;
            const pageCenterY = pageRect.height / 2;

            let snappedX = x;
            let snappedY = y;
            let snapped = false;

            if (snapToVerticalLine) {
                if (Math.abs(elementLeft - pageCenterX) < snapThreshold) {
                    snappedX = pageCenterX;
                    snapped = true;
                }
                else if (Math.abs(elementRight - pageCenterX) < snapThreshold) {
                    snappedX = pageCenterX - elementWidth;
                    snapped = true;
                }
                else if (Math.abs(elementCenterX - pageCenterX) < snapThreshold) {
                    snappedX = pageCenterX - elementWidth / 2;
                    snapped = true;
                }
            }

            if (snapToHorizontalLine) {
                if (Math.abs(elementTop - pageCenterY) < snapThreshold) {
                    snappedY = pageCenterY;
                    snapped = true;
                }
                else if (Math.abs(elementBottom - pageCenterY) < snapThreshold) {
                    snappedY = pageCenterY - elementHeight;
                    snapped = true;
                }
                else if (Math.abs(elementCenterY - pageCenterY) < snapThreshold) {
                    snappedY = pageCenterY - elementHeight / 2;
                    snapped = true;
                }
            }

            return { x: snappedX, y: snappedY, snapped: snapped };
        }

        function applyElementSnapping(x, y, draggedElement, page) {
            const pageRect = page.getBoundingClientRect();
            const draggedRect = draggedElement.getBoundingClientRect();
            const draggedWidth = draggedRect.width;
            const draggedHeight = draggedRect.height;

            const draggedLeft = x;
            const draggedRight = x + draggedWidth;
            const draggedCenterX = x + draggedWidth / 2;
            const draggedTop = y;
            const draggedBottom = y + draggedHeight;
            const draggedCenterY = y + draggedHeight / 2;

            let snappedX = x;
            let snappedY = y;
            let snapped = false;

            const otherElements = Array.from(page.querySelectorAll('.element')).filter(el => el !== draggedElement);

            let minXDistance = Infinity;
            let minYDistance = Infinity;
            let bestXSnap = x;
            let bestYSnap = y;
            let snapXPosition = null;
            let snapYPosition = null;
            let snapTargetElement = null;

            otherElements.forEach(otherElement => {
                const otherRect = otherElement.getBoundingClientRect();
                const otherX = parseFloat(otherElement.getAttribute('data-x')) || 0;
                const otherY = parseFloat(otherElement.getAttribute('data-y')) || 0;

                const otherWidth = otherRect.width;
                const otherHeight = otherRect.height;

                const otherLeft = otherX;
                const otherRight = otherX + otherWidth;
                const otherCenterX = otherX + otherWidth / 2;
                const otherTop = otherY;
                const otherBottom = otherY + otherHeight;
                const otherCenterY = otherY + otherHeight / 2;

                const xSnapPoints = [
                    { distance: Math.abs(draggedLeft - otherLeft), snap: otherLeft, position: otherLeft },
                    { distance: Math.abs(draggedLeft - otherRight), snap: otherRight, position: otherRight },
                    { distance: Math.abs(draggedRight - otherLeft), snap: otherLeft - draggedWidth, position: otherLeft },
                    { distance: Math.abs(draggedRight - otherRight), snap: otherRight - draggedWidth, position: otherRight },
                    { distance: Math.abs(draggedCenterX - otherCenterX), snap: otherCenterX - draggedWidth / 2, position: otherCenterX },
                ];

                xSnapPoints.forEach(point => {
                    if (point.distance < minXDistance && point.distance < snapThreshold) {
                        minXDistance = point.distance;
                        bestXSnap = point.snap;
                        snapXPosition = point.position;
                        snapTargetElement = otherElement;
                    }
                });

                const ySnapPoints = [
                    { distance: Math.abs(draggedTop - otherTop), snap: otherTop, position: otherTop },
                    { distance: Math.abs(draggedTop - otherBottom), snap: otherBottom, position: otherBottom },
                    { distance: Math.abs(draggedBottom - otherTop), snap: otherTop - draggedHeight, position: otherTop },
                    { distance: Math.abs(draggedBottom - otherBottom), snap: otherBottom - draggedHeight, position: otherBottom },
                    { distance: Math.abs(draggedCenterY - otherCenterY), snap: otherCenterY - draggedHeight / 2, position: otherCenterY },
                ];

                ySnapPoints.forEach(point => {
                    if (point.distance < minYDistance && point.distance < snapThreshold) {
                        minYDistance = point.distance;
                        bestYSnap = point.snap;
                        snapYPosition = point.position;
                        if (!snapTargetElement) snapTargetElement = otherElement;
                    }
                });
            });

            if (minXDistance < snapThreshold) {
                snappedX = bestXSnap;
                snapped = true;
            }

            if (minYDistance < snapThreshold) {
                snappedY = bestYSnap;
                snapped = true;
            }

            return {
                x: snappedX,
                y: snappedY,
                snapped: snapped,
                snapInfo: snapped ? {
                    xPosition: minXDistance < snapThreshold ? snapXPosition : null,
                    yPosition: minYDistance < snapThreshold ? snapYPosition : null,
                    targetElement: snapTargetElement
                } : null
            };
        }

        function showSnapGuides(snapInfo) {
            page.querySelectorAll('.element.snap-target').forEach(el => {
                el.classList.remove('snap-target');
            });

            if (snapInfo.xPosition !== null) {
                snapLineX.style.left = snapInfo.xPosition + 'px';
                snapLineX.style.display = 'block';
            } else {
                snapLineX.style.display = 'none';
            }

            if (snapInfo.yPosition !== null) {
                snapLineY.style.top = snapInfo.yPosition + 'px';
                snapLineY.style.display = 'block';
            } else {
                snapLineY.style.display = 'none';
            }

            if (snapInfo.targetElement) {
                snapInfo.targetElement.classList.add('snap-target');
            }
        }

        function hideSnapGuides() {
            snapLineX.style.display = 'none';
            snapLineY.style.display = 'none';

            page.querySelectorAll('.element.snap-target').forEach(el => {
                el.classList.remove('snap-target');
            });
        }

        function stopDrag() {
            isDragging = false;
            element.classList.remove('dragging');
            element.classList.remove('element-snapped');
            hideSnapGuides();
            document.removeEventListener('mousemove', drag);
            document.removeEventListener('mouseup', stopDrag);
        }
    });
    /* @DLH.NewPdfElement DRAG AND DROP, SNAPPING LOGIC*/

    /* COPY LOGIC FOR @DLH.NewPdfElement */
    document.addEventListener('contextmenu', function (e) {
        const element = e.target.closest('.element');

        if (element) {
            e.preventDefault();

            const x = element.getAttribute('data-x');
            const y = element.getAttribute('data-y');

            const textToCopy = `@DLH.NewPdfElement(x: ${x}, y:  ${y})`;

            navigator.clipboard.writeText(textToCopy).then(() => {
                showToast('Copied! ', e.clientX, e.clientY);
            }).catch(err => {
                showToast('Failed to copy location!! ', e.clientX, e.clientY);
            });
        }
    });

    function showToast(message, mouseX, mouseY) {
        const toast = document.createElement('div');
        toast.textContent = message;
        toast.style.cssText = `
        position: fixed;
        left: ${mouseX}px;
        top: ${mouseY}px;
        background-color: #333;
        color: white;
        padding: 12px 24px;
        border-radius: 4px;
        font-size: 14px;
        z-index: 10000;
        opacity: 0;
        transition: opacity 0.3s ease;
        transform: translate(-50%, -100%);
        margin-top: -10px;
        pointer-events: none;
    `;

        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '1';
        }, 10);

        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => {
                document.body.removeChild(toast);
            }, 300);
        }, 1000);
    }
    /* COPY LOGIC FOR @DLH.NewPdfElement */

    /* CENTERED SNAPPING LINES */
    $(document).on('keydown', function (e) {
        if (e.ctrlKey && e.key === 'm') {
            e.preventDefault();
            $('.vertical-middle-line, .horizontal-middle-line').toggle();
        }
    });
    /* CENTERED SNAPPING LINES */
});