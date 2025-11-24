dataLinq.events.on('onpageloaded', function () {

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

const copyButtons = document.querySelectorAll('.copy-btn');

copyButtons.forEach(button => {
    button.addEventListener('click', async function () {
        const pageWrapper = this.closest('.page-wrapper');
        const page = pageWrapper.querySelector('.page');

        const pageClone = page.cloneNode(true);

        const elements = pageClone.querySelectorAll('.element');

        elements.forEach(element => {
            const commentMatch = element.innerHTML.match(/<!--([\s\S]*?)-->/);

            if (commentMatch) {
                const commentText = commentMatch[1].trim();
                element.innerHTML = `<!--${commentText.replace("@", "@@")}-->\n${commentText}`;
            }
        });

        const contentToCopy = pageClone.innerHTML;

        try {
            await navigator.clipboard.writeText(contentToCopy);

            const originalText = this.textContent;
            this.textContent = 'Copied!';
            this.classList.add('copied');

            setTimeout(() => {
                this.textContent = originalText;
                this.classList.remove('copied');
            }, 2000);
        } catch (err) {
            console.error('Failed to copy:', err);
            this.textContent = 'Failed to copy';
            setTimeout(() => {
                this.textContent = 'Copy HTML';
            }, 2000);
        }
    });
});

$(document).on('keydown', function (e) {
    if (e.ctrlKey && e.key === 'm') {
        e.preventDefault();
        $('.vertical-middle-line, .horizontal-middle-line').toggle();
    }
});

    initializeTemplateLoader();

    splitAllTables();
});

const urlParams = new URLSearchParams(window.location.search);
if (urlParams.get('print') === 'true') {
    dataLinq.events.on('onpageloaded', function () {
        setTimeout(function () {
            window.print();
        }, 2000);
    });
}

function addPageNumbers(options) {
    const { type = 0, skipPages = 0, position = 0 } = options;

    const pages = document.querySelectorAll('.page');
    const total = pages.length;

    pages.forEach((page, index) => {
        const p = document.createElement('p');

        const currentPage = index + 1 - skipPages;

        if (index < skipPages) return;

        p.textContent = type === 0
            ? `${currentPage}/${total - skipPages}`
            : `${currentPage}`;

        p.classList.add('page-number', `position-${position}`);

        page.appendChild(p);
    });
}

function initializeTemplateLoader() {
    const pages = document.querySelectorAll('.page');

    const processedTemplates = new Set();

    const checkedPages = new Set();

    pages.forEach((page, index) => {
        if (checkedPages.has(page)) {
            return;
        }

        checkedPages.add(page);

        const templateName = page.getAttribute('datalinq-pdfreport-template');

        if (templateName) {
            if (processedTemplates.has(templateName)) {
            } else {
                processedTemplates.add(templateName);

                const pagesWithTemplate = [page];
                const duplicatePages = checkForDuplicates(pages, templateName, checkedPages, index);
                pagesWithTemplate.push(...duplicatePages);

                makeTemplateRequest(pagesWithTemplate, templateName);
            }
        }
    });

}

function checkForDuplicates(pages, templateName, checkedPages, currentIndex) {
    const duplicates = [];
    const duplicatePages = [];

    pages.forEach((page, index) => {
        if (index <= currentIndex || checkedPages.has(page)) {
            return;
        }

        const pageTemplateName = page.getAttribute('datalinq-pdfreport-template');

        if (pageTemplateName === templateName) {
            duplicates.push(index + 1);
            duplicatePages.push(page);
            checkedPages.add(page);
        }
    });

    return duplicatePages;
}

function makeTemplateRequest(pagesArray, templateName) {
    dataLinq.getTemplate(templateName).then(function (data) {
        console.log('INDIVIDUAL FETCH HAPPENING');
        pagesArray.forEach(function (page) {
            $(page).append(data);
        });
    }).catch(function (error) {
        console.error(`Error loading template "${templateName}":`, error);
    });
}

function splitAllTables() {
    const pagesContainer = document.getElementById('pagesContainer');
    const originalPageWrappers = Array.from(pagesContainer.querySelectorAll('.page-wrapper'));

    originalPageWrappers.forEach(pageWrapper => {
        const page = pageWrapper.querySelector('.page');
        const tables = page.querySelectorAll('table');

        tables.forEach(table => {
            splitTable(table, page, pageWrapper);
        });
    });
}

function splitTable(table, page, originalPageWrapper) {
    const tbody = table.querySelector('tbody');
    if (!tbody) return;

    const rows = Array.from(tbody.querySelectorAll('tr'));
    if (rows.length === 0) return;

    if (table.dataset.processed === 'true') return;
    table.dataset.processed = 'true';

    const pageHeight = page.offsetHeight;
    const topPadding = 30;
    const bottomPadding = 20;

    let contentBeforeTable = 0;
    let sibling = table.previousElementSibling;
    while (sibling) {
        contentBeforeTable += sibling.offsetHeight;
        const siblingStyles = window.getComputedStyle(sibling);
        contentBeforeTable += parseFloat(siblingStyles.marginTop) || 0;
        contentBeforeTable += parseFloat(siblingStyles.marginBottom) || 0;
        sibling = sibling.previousElementSibling;
    }

    const tableStyles = window.getComputedStyle(table);
    const tableMarginTop = parseFloat(tableStyles.marginTop) || 0;
    const tableMarginBottom = parseFloat(tableStyles.marginBottom) || 0;

    let thead = table.querySelector('thead');
    let headerRow = null;
    let theadHeight = 0;
    let startRowIndex = 0;

    if (thead) {
        theadHeight = thead.offsetHeight;
    } else {
        const firstRow = rows[0];
        if (firstRow && firstRow.querySelector('th')) {
            headerRow = firstRow;
            theadHeight = headerRow.offsetHeight;
            startRowIndex = 1;
            headerRow.dataset.isHeader = 'true';
        }
    }

    const dataRows = rows.slice(startRowIndex);

    const pagePadding = 40;
    const firstPageAvailable = pageHeight - contentBeforeTable - theadHeight - tableMarginTop - tableMarginBottom - pagePadding;
    const continuationAvailable = pageHeight - theadHeight - topPadding - bottomPadding - pagePadding;

    const singleRowHeight = dataRows[0].offsetHeight;

    const rowsInFirstPage = Math.floor(firstPageAvailable / singleRowHeight);
    const rowsPerContinuation = Math.floor(continuationAvailable / singleRowHeight);

    if (rowsInFirstPage >= dataRows.length) {
        return;
    }

    for (let i = rowsInFirstPage; i < dataRows.length; i++) {
        dataRows[i].classList.add('hidden');
    }

    const wrapperElements = [];
    let currentElement = table.parentElement;

    console.log('Starting wrapper collection from table parent:', currentElement);

    while (currentElement && !currentElement.classList.contains('page')) {
        console.log('Found wrapper:', currentElement.tagName, currentElement.className, currentElement.getAttribute('style'));
        wrapperElements.unshift(currentElement);
        currentElement = currentElement.parentElement;
    }

    console.log('Total wrappers found:', wrapperElements.length);

    let remainingRows = dataRows.slice(rowsInFirstPage);
    const newPages = [];

    while (remainingRows.length > 0) {
        const rowsForThisPage = remainingRows.slice(0, rowsPerContinuation);
        remainingRows = remainingRows.slice(rowsPerContinuation);

        const newPageWrapper = createContinuationPage(rowsForThisPage, table, thead, headerRow, wrapperElements);
        newPages.push(newPageWrapper);
    }

    const pagesContainer = document.getElementById('pagesContainer');
    let insertAfter = originalPageWrapper;

    newPages.forEach(newPageWrapper => {
        const nextElement = insertAfter.nextElementSibling;
        if (nextElement) {
            pagesContainer.insertBefore(newPageWrapper, nextElement);
        } else {
            pagesContainer.appendChild(newPageWrapper);
        }
        insertAfter = newPageWrapper;
    });
}

function createContinuationPage(rows, originalTable, thead, headerRow, wrapperElements) {
    console.log('Creating continuation page with', wrapperElements.length, 'wrappers');

    const newPageWrapper = document.createElement('div');
    newPageWrapper.className = 'page-wrapper';

    const newPage = document.createElement('div');
    newPage.className = 'page';

    let currentParent = newPage;

    wrapperElements.forEach((wrapper, index) => {
        console.log(`Cloning wrapper ${index}:`, wrapper.tagName, wrapper.className);

        const clonedWrapper = document.createElement(wrapper.tagName);

        if (wrapper.className) {
            clonedWrapper.className = wrapper.className;
        }

        const inlineStyle = wrapper.getAttribute('style');
        if (inlineStyle) {
            clonedWrapper.setAttribute('style', inlineStyle);
            console.log('  - Copied inline style:', inlineStyle);
        }

        Array.from(wrapper.attributes).forEach(attr => {
            if (attr.name === 'class' || attr.name === 'style') {
                return;
            }

            if (attr.name.startsWith('data-')) {
                clonedWrapper.setAttribute(attr.name, attr.value);
            }

            if (attr.name === 'id') {

            }
        });

        currentParent.appendChild(clonedWrapper);
        currentParent = clonedWrapper;
    });

    const newTable = document.createElement('table');
    newTable.dataset.processed = 'true';

    Array.from(originalTable.classList).forEach(cls => {
        if (cls !== 'first-table') {
            newTable.classList.add(cls);
        }
    });

    const tableInlineStyle = originalTable.getAttribute('style');
    if (tableInlineStyle) {
        const styleWithoutMargin = tableInlineStyle.replace(/margin-top\s*:\s*[^;]+;?/gi, '');
        if (styleWithoutMargin.trim()) {
            newTable.setAttribute('style', styleWithoutMargin);
        }
    }

    Array.from(originalTable.attributes).forEach(attr => {
        if (attr.name !== 'class' &&
            attr.name !== 'style' &&
            attr.name !== 'id' &&
            !attr.name.startsWith('data-')) {
            newTable.setAttribute(attr.name, attr.value);
        }
    });

    if (thead) {
        const newThead = thead.cloneNode(true);
        newTable.appendChild(newThead);
    }

    const tbody = document.createElement('tbody');

    if (headerRow && !thead) {
        const clonedHeaderRow = headerRow.cloneNode(true);
        clonedHeaderRow.classList.remove('hidden');
        clonedHeaderRow.removeAttribute('data-is-header');
        tbody.appendChild(clonedHeaderRow);
    }

    rows.forEach(row => {
        if (row.dataset.isHeader === 'true') {
            return;
        }

        const clonedRow = row.cloneNode(true);
        clonedRow.classList.remove('hidden');
        tbody.appendChild(clonedRow);
    });

    newTable.appendChild(tbody);

    currentParent.appendChild(newTable);

    newPageWrapper.appendChild(newPage);

    console.log('Continuation page created, final structure:', newPageWrapper);

    return newPageWrapper;
}