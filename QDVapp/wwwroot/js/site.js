// Please see documentation at https://learn.microsoft.com//aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    document.querySelectorAll('.upload-dropzone').forEach(function (zone) {
        var fileInput = zone.querySelector('input[type="file"]');
        var contentEl = zone.querySelector('.upload-dropzone-content');
        var progressEl = zone.querySelector('.upload-dropzone-progress');
        var resultEl = zone.querySelector('.upload-dropzone-result');
        var uploadUrl = zone.getAttribute('data-upload-url');

        zone.addEventListener('click', function (e) {
            if (e.target === fileInput) return;
            fileInput.click();
        });

        zone.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.add('dragover');
        });

        zone.addEventListener('dragleave', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.remove('dragover');
        });

        zone.addEventListener('drop', function (e) {
            e.preventDefault();
            e.stopPropagation();
            zone.classList.remove('dragover');

            var files = e.dataTransfer.files;
            if (files.length > 0) {
                uploadFile(files[0]);
            }
        });

        fileInput.addEventListener('change', function () {
            if (fileInput.files.length > 0) {
                uploadFile(fileInput.files[0]);
            }
        });

        function uploadFile(file) {
            if (!file.name.toLowerCase().endsWith('.xlsx')) {
                showResult('Seuls les fichiers .xlsx sont acceptés.', 'danger');
                return;
            }

            contentEl.classList.add('d-none');
            resultEl.classList.add('d-none');
            progressEl.classList.remove('d-none');

            var formData = new FormData();
            formData.append('file', file);

            var token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                formData.append('__RequestVerificationToken', token.value);
            }

            fetch(uploadUrl, {
                method: 'POST',
                body: formData
            })
            .then(function (response) {
                return response.json().then(function (data) {
                    return { ok: response.ok, data: data };
                });
            })
            .then(function (result) {
                progressEl.classList.add('d-none');
                if (result.ok && result.data.success) {
                    showResult('Fichier importé avec succès ! Rechargement...', 'success');
                    setTimeout(function () {
                        location.reload();
                    }, 1500);
                } else {
                    var msg = (result.data && result.data.error) ? result.data.error : 'Erreur lors de l\'import.';
                    showResult(msg, 'danger');
                    resetZone();
                }
            })
            .catch(function () {
                progressEl.classList.add('d-none');
                showResult('Erreur réseau lors de l\'import.', 'danger');
                resetZone();
            });
        }

        function showResult(message, type) {
            resultEl.className = 'upload-dropzone-result alert alert-' + type;
            resultEl.textContent = message;
            resultEl.classList.remove('d-none');
        }

        function resetZone() {
            setTimeout(function () {
                resultEl.classList.add('d-none');
                contentEl.classList.remove('d-none');
                fileInput.value = '';
            }, 4000);
        }
    });
})();

// Gantt page features
(function () {
    var side = document.getElementById('ganttSide');
    var timeline = document.getElementById('ganttTimeline');
    if (!side || !timeline) return;

    var sideRows = document.getElementById('ganttSideRows');
    var timelineRows = document.getElementById('ganttTimelineRows');
    var searchInput = document.getElementById('ganttSearch');
    var groupSelect = document.getElementById('ganttGroupBy');
    var zoomSelect = document.getElementById('ganttZoom');

    // Sync scroll
    var syncing = false;
    side.addEventListener('scroll', function () {
        if (syncing) return;
        syncing = true;
        timeline.scrollTop = side.scrollTop;
        syncing = false;
    });
    timeline.addEventListener('scroll', function () {
        if (syncing) return;
        syncing = true;
        side.scrollTop = timeline.scrollTop;
        syncing = false;
    });

    // Search filter
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            applyGanttFilters();
        });
    }

    function applyGanttFilters() {
        var q = (searchInput ? searchInput.value : '').trim().toLowerCase();
        var sideRowEls = sideRows.querySelectorAll('.gantt-data-row');
        var tlRowEls = timelineRows.querySelectorAll('.gantt-data-row');
        var groupHeaders = sideRows.querySelectorAll('.gantt-group-header');

        sideRowEls.forEach(function (row, i) {
            var match = !q || (row.dataset.search || '').indexOf(q) !== -1;
            var groupKey = row.dataset.group || '';
            var inCollapsedGroup = false;
            if (groupKey) {
                var hdr = sideRows.querySelector('.gantt-group-header[data-group="' + groupKey + '"]');
                if (hdr && hdr.classList.contains('collapsed')) inCollapsedGroup = true;
            }
            row.style.display = (match && !inCollapsedGroup) ? '' : 'none';
            if (tlRowEls[i]) tlRowEls[i].style.display = (match && !inCollapsedGroup) ? '' : 'none';
        });
    }

    // Grouping
    if (groupSelect) {
        groupSelect.addEventListener('change', function () {
            applyGanttGrouping(this.value);
        });
    }

    function applyGanttGrouping(mode) {
        removeGanttGrouping();
        if (!mode) {
            applyGanttFilters();
            return;
        }

        var sideRowEls = Array.from(sideRows.querySelectorAll('.gantt-data-row'));
        var tlRowEls = Array.from(timelineRows.querySelectorAll('.gantt-data-row'));
        var attr = mode === 'statut' ? 'data-statut' : 'data-vendeur';
        var groups = {};
        var order = [];

        sideRowEls.forEach(function (row, i) {
            var key = row.getAttribute(attr) || '(vide)';
            if (!groups[key]) { groups[key] = []; order.push(key); }
            groups[key].push({ side: row, timeline: tlRowEls[i] });
        });

        order.forEach(function (key) {
            var count = groups[key].length;

            var sideHeader = document.createElement('div');
            sideHeader.className = 'gantt-group-header';
            sideHeader.setAttribute('data-group', key);
            sideHeader.innerHTML = '<span class="group-toggle">&#9660;</span> ' + key + ' <span class="badge bg-secondary">' + count + '</span>';
            sideHeader.addEventListener('click', function () {
                this.classList.toggle('collapsed');
                var collapsed = this.classList.contains('collapsed');
                var toggle = this.querySelector('.group-toggle');
                toggle.innerHTML = collapsed ? '&#9654;' : '&#9660;';
                applyGanttFilters();
            });
            sideRows.appendChild(sideHeader);

            groups[key].forEach(function (g) {
                g.side.dataset.group = key;
                sideRows.appendChild(g.side);
                if (g.timeline) {
                    g.timeline.dataset.group = key;
                    timelineRows.appendChild(g.timeline);
                }
            });
        });

        applyGanttFilters();
    }

    function removeGanttGrouping() {
        sideRows.querySelectorAll('.gantt-group-header').forEach(function (h) { h.remove(); });
        sideRows.querySelectorAll('.gantt-data-row').forEach(function (r) { delete r.dataset.group; });
        timelineRows.querySelectorAll('.gantt-data-row').forEach(function (r) { delete r.dataset.group; });
    }

    // Zoom
    if (zoomSelect) {
        var zoomLevels = { day: 30, week: 6, month: 1 };
        zoomSelect.addEventListener('change', function () {
            var pxPerDay = zoomLevels[this.value] || 6;
            var container = document.getElementById('ganttContainer');
            var totalDays = parseInt(container.getAttribute('data-total-days')) || 120;
            var newWidth = totalDays * pxPerDay;
            var header = document.getElementById('ganttTimelineHeader');
            var rows = document.getElementById('ganttTimelineRows');
            if (header) header.style.width = newWidth + 'px';
            if (rows) rows.style.width = newWidth + 'px';
        });
    }
})();
