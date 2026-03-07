document.addEventListener("DOMContentLoaded", function () {
    const bellButton = document.getElementById("notificationBellButton");
    const popover = document.getElementById("notificationPopover");
    if (!bellButton || !popover) {
        return;
    }

    const unreadDot = document.getElementById("notificationUnreadDot");
    const list = document.getElementById("notificationList");
    const emptyState = document.getElementById("notificationEmptyState");
    const emptyText = document.getElementById("notificationEmptyText");
    const tabButtons = document.querySelectorAll(".notification-tab");
    let activeTab = "all";
    let allNotifications = [];

    function escapeHtml(value) {
        return (value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function formatRelativeTime(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        const diffSeconds = Math.max(1, Math.floor((Date.now() - date.getTime()) / 1000));
        if (diffSeconds < 60) {
            return "Just now";
        }

        const diffMinutes = Math.floor(diffSeconds / 60);
        if (diffMinutes < 60) {
            return diffMinutes + "m ago";
        }

        const diffHours = Math.floor(diffMinutes / 60);
        if (diffHours < 24) {
            return diffHours + "h ago";
        }

        const diffDays = Math.floor(diffHours / 24);
        return diffDays + "d ago";
    }

    async function markAsRead(notificationId) {
        if (!notificationId) {
            return;
        }

        try {
            await fetch("/notifications/" + notificationId + "/read", {
                method: "POST",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
        } catch (error) {
            console.error("Unable to mark notification as read.", error);
        }
    }

    function render() {
        const filtered = activeTab === "unread"
            ? allNotifications.filter(item => !item.isRead)
            : allNotifications;

        if (filtered.length === 0) {
            list.innerHTML = "";
            emptyState.classList.remove("d-none");
            emptyText.textContent = activeTab === "unread"
                ? "You have no unread notifications"
                : "You have no notifications";
        } else {
            emptyState.classList.add("d-none");
            list.innerHTML = filtered.map(item => {
                const itemClass = item.isRead ? "notification-item" : "notification-item unread";
                const link = item.actionUrl ? ` data-url="${escapeHtml(item.actionUrl)}"` : "";

                return `<button type="button" class="${itemClass}" data-id="${item.id}"${link}>
                    <div class="notification-item-title">${escapeHtml(item.title)}</div>
                    <div class="notification-item-message">${escapeHtml(item.message)}</div>
                    <div class="notification-item-time">${formatRelativeTime(item.createdAt)}</div>
                </button>`;
            }).join("");
        }

        const unreadCount = allNotifications.filter(item => !item.isRead).length;
        if (unreadCount > 0) {
            unreadDot.classList.remove("d-none");
        } else {
            unreadDot.classList.add("d-none");
        }
    }

    async function fetchNotifications() {
        try {
            const response = await fetch("/notifications/list", { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) {
                return;
            }

            const payload = await response.json();
            allNotifications = Array.isArray(payload.notifications) ? payload.notifications : [];
            render();
        } catch (error) {
            console.error("Unable to fetch notifications.", error);
        }
    }

    bellButton.addEventListener("click", function (event) {
        event.preventDefault();
        event.stopPropagation();

        const isHidden = popover.classList.contains("d-none");
        if (isHidden) {
            popover.classList.remove("d-none");
            popover.setAttribute("aria-hidden", "false");
            fetchNotifications();
        } else {
            popover.classList.add("d-none");
            popover.setAttribute("aria-hidden", "true");
        }
    });

    document.addEventListener("click", function (event) {
        if (!popover.classList.contains("d-none") && !popover.contains(event.target) && !bellButton.contains(event.target)) {
            popover.classList.add("d-none");
            popover.setAttribute("aria-hidden", "true");
        }
    });

    tabButtons.forEach(function (tabButton) {
        tabButton.addEventListener("click", function () {
            activeTab = tabButton.getAttribute("data-tab") === "unread" ? "unread" : "all";
            tabButtons.forEach(btn => btn.classList.remove("active"));
            tabButton.classList.add("active");
            render();
        });
    });

    list.addEventListener("click", async function (event) {
        const targetItem = event.target.closest(".notification-item");
        if (!targetItem) {
            return;
        }

        const notificationId = targetItem.getAttribute("data-id");
        const actionUrl = targetItem.getAttribute("data-url");
        const selected = allNotifications.find(item => String(item.id) === String(notificationId));
        if (selected && !selected.isRead) {
            selected.isRead = true;
            render();
            await markAsRead(notificationId);
        }

        if (actionUrl) {
            window.location.href = actionUrl;
        }
    });

    fetchNotifications();
    setInterval(fetchNotifications, 30000);
});

document.addEventListener("DOMContentLoaded", function () {
    let overlay = document.getElementById("globalLoadingOverlay");
    if (!overlay) {
        overlay = document.createElement("div");
        overlay.id = "globalLoadingOverlay";
        overlay.className = "global-loading-overlay";
        overlay.innerHTML = '<div class="global-loading-spinner" aria-hidden="true"></div>';
        document.body.appendChild(overlay);
    }

    function showLoading() {
        overlay.classList.add("visible");
        window.setTimeout(function () {
            overlay.classList.remove("visible");
        }, 10000);
    }

    function isInternalNavigation(anchor) {
        const href = anchor.getAttribute("href");
        if (!href || href.startsWith("#") || href.startsWith("javascript:") || href.startsWith("mailto:") || href.startsWith("tel:")) {
            return false;
        }

        if (anchor.getAttribute("target") === "_blank" || anchor.hasAttribute("download")) {
            return false;
        }

        try {
            const url = new URL(anchor.href, window.location.origin);
            return url.origin === window.location.origin;
        } catch {
            return false;
        }
    }

    document.addEventListener("click", function (event) {
        const anchor = event.target.closest("a[href]");
        if (!anchor || !isInternalNavigation(anchor)) {
            return;
        }

        showLoading();
    });

    document.addEventListener("submit", function (event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        if (form.target === "_blank") {
            return;
        }

        showLoading();
    });

    window.addEventListener("beforeunload", function () {
        showLoading();
    });
});

document.addEventListener("DOMContentLoaded", function () {
    let pendingArchiveForm = null;
    const modalId = "archiveConfirmModal";

    function ensureArchiveModal() {
        let modalElement = document.getElementById(modalId);
        if (modalElement) {
            return modalElement;
        }

        modalElement = document.createElement("div");
        modalElement.className = "modal fade";
        modalElement.id = modalId;
        modalElement.tabIndex = -1;
        modalElement.setAttribute("aria-hidden", "true");
        modalElement.innerHTML = `
            <div class="modal-dialog modal-dialog-centered syncora-confirm-dialog">
                <div class="modal-content border-0 shadow-lg syncora-confirm-content">
                    <div class="modal-header border-0 pb-0">
                        <h5 class="modal-title fw-bold" id="archiveConfirmTitle">Archive Item</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body pt-3">
                        <p class="mb-3" id="archiveConfirmMessage">Archive this item?</p>
                        <div class="d-flex justify-content-end gap-2">
                            <button type="button" class="btn btn-outline-secondary px-4" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-dark px-4" id="archiveConfirmSubmit">Archive</button>
                        </div>
                    </div>
                </div>
            </div>`;

        document.body.appendChild(modalElement);
        return modalElement;
    }

    document.addEventListener("submit", function (event) {
        const form = event.target.closest("form.archive-confirm-form");
        if (!form) {
            return;
        }

        if (form.dataset.archiveConfirmed === "true") {
            form.dataset.archiveConfirmed = "";
            return;
        }

        event.preventDefault();
        pendingArchiveForm = form;

        const modalElement = ensureArchiveModal();
        const title = form.getAttribute("data-archive-title") || "Archive Item";
        const message = form.getAttribute("data-archive-message") || "Archive this item?";

        const titleNode = modalElement.querySelector("#archiveConfirmTitle");
        const messageNode = modalElement.querySelector("#archiveConfirmMessage");
        if (titleNode) {
            titleNode.textContent = title;
        }
        if (messageNode) {
            messageNode.textContent = message;
        }

        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        modal.show();
    });

    document.addEventListener("click", function (event) {
        const button = event.target.closest("#archiveConfirmSubmit");
        if (!button || !pendingArchiveForm) {
            return;
        }

        pendingArchiveForm.dataset.archiveConfirmed = "true";
        pendingArchiveForm.submit();
    });
});

document.addEventListener("DOMContentLoaded", function () {
    const searchInputs = document.querySelectorAll("form[method='get'] input[name='search']");
    if (!searchInputs.length) {
        return;
    }

    function getLiveSearchConfig() {
        const path = window.location.pathname.toLowerCase();
        if (path.includes("/projects")) {
            return { itemSelector: ".projects-grid .project-card", rootSelector: ".projects-container", emptyText: "No projects found." };
        }
        if (path.includes("/tasks")) {
            return { itemSelector: ".tasks-board .task-card", rootSelector: ".tasks-container", emptyText: "No tasks found." };
        }
        if (path.includes("/documents")) {
            return { itemSelector: ".doc-grid .doc-card", rootSelector: ".docs-container", emptyText: "No documents found." };
        }
        if (path.includes("/employees")) {
            return { itemSelector: ".emp-table-card tbody tr", rootSelector: ".employees-container", emptyText: "No employees found.", tableMode: true, tableCols: 6 };
        }
        if (path.includes("/usermanagement")) {
            return { itemSelector: "#umUsersTableBody tr[data-um-row='true']", rootSelector: ".um-container", emptyText: "No users found.", tableMode: true, tableCols: 5 };
        }
        if (path.includes("/auditlogs")) {
            return { itemSelector: ".audit-table-card tbody tr", rootSelector: ".audit-container", emptyText: "No logs found.", tableMode: true, tableCols: 5 };
        }
        if (path.includes("/archive")) {
            return { itemSelector: ".archive-list .archive-item-card", rootSelector: ".archive-container", emptyText: "No archived items found." };
        }

        return null;
    }

    function isDataRow(item) {
        const firstCell = item.querySelector("td[colspan]");
        return !firstCell;
    }

    function ensureNoResultsElement(config) {
        if (!config) {
            return null;
        }

        if (config.tableMode) {
            const tableBody = document.querySelector(config.itemSelector)?.closest("tbody");
            if (!tableBody) {
                return null;
            }

            let noResultsRow = tableBody.querySelector("tr[data-live-search-empty='true']");
            if (!noResultsRow) {
                noResultsRow = document.createElement("tr");
                noResultsRow.setAttribute("data-live-search-empty", "true");
                noResultsRow.classList.add("d-none");
                noResultsRow.innerHTML = `<td colspan="${config.tableCols || 1}" class="text-center text-muted py-4">${config.emptyText}</td>`;
                tableBody.appendChild(noResultsRow);
            }

            return noResultsRow;
        }

        const root = document.querySelector(config.rootSelector);
        if (!root) {
            return null;
        }

        let noResults = root.querySelector("[data-live-search-empty='true']");
        if (!noResults) {
            noResults = document.createElement("div");
            noResults.setAttribute("data-live-search-empty", "true");
            noResults.className = "empty-state-section text-center py-5 bg-white rounded-4 border d-none";
            noResults.innerHTML = `<h5 class="mb-2">${config.emptyText}</h5>`;
            root.appendChild(noResults);
        }

        return noResults;
    }

    const config = getLiveSearchConfig();
    if (!config) {
        return;
    }

    const noResultsElement = ensureNoResultsElement(config);
    const DEBOUNCE_MS = 200;
    let debounceTimer = null;

    function applyFilter(term) {
        const normalized = (term || "").trim().toLowerCase();
        const items = Array.from(document.querySelectorAll(config.itemSelector))
            .filter(function (item) {
                return config.tableMode ? isDataRow(item) : true;
            });

        let visibleCount = 0;

        items.forEach(function (item) {
            const text = (item.textContent || "").toLowerCase();
            const visible = !normalized || text.includes(normalized);
            item.classList.toggle("d-none", !visible);
            if (visible) {
                visibleCount += 1;
            }
        });

        if (noResultsElement) {
            noResultsElement.classList.toggle("d-none", visibleCount > 0);
        }
    }

    searchInputs.forEach(function (input) {
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
            }
        });

        input.addEventListener("input", function () {
            if (debounceTimer) {
                clearTimeout(debounceTimer);
            }

            debounceTimer = setTimeout(function () {
                applyFilter(input.value);
            }, DEBOUNCE_MS);
        });
    });
});

document.addEventListener("DOMContentLoaded", function () {
    let pendingLogoutForm = null;
    const modalId = "logoutConfirmModal";

    function ensureLogoutModal() {
        let modalElement = document.getElementById(modalId);
        if (modalElement) {
            return modalElement;
        }

        modalElement = document.createElement("div");
        modalElement.className = "modal fade";
        modalElement.id = modalId;
        modalElement.tabIndex = -1;
        modalElement.setAttribute("aria-hidden", "true");
        modalElement.innerHTML = `
            <div class="modal-dialog modal-dialog-centered syncora-confirm-dialog">
                <div class="modal-content border-0 shadow-lg syncora-confirm-content">
                    <div class="modal-header border-0 pb-0">
                        <h5 class="modal-title fw-bold" id="logoutConfirmTitle">Confirm Logout</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body pt-3">
                        <p class="mb-3" id="logoutConfirmMessage">Are you sure you want to log out?</p>
                        <div class="d-flex justify-content-end gap-2">
                            <button type="button" class="btn btn-outline-secondary px-4" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-dark px-4" id="logoutConfirmSubmit">Logout</button>
                        </div>
                    </div>
                </div>
            </div>`;

        document.body.appendChild(modalElement);
        return modalElement;
    }

    document.addEventListener("submit", function (event) {
        const form = event.target.closest("form.logout-confirm-form");
        if (!form) {
            return;
        }

        if (form.dataset.logoutConfirmed === "true") {
            form.dataset.logoutConfirmed = "";
            return;
        }

        event.preventDefault();
        pendingLogoutForm = form;

        const modalElement = ensureLogoutModal();
        const title = form.getAttribute("data-logout-title") || "Confirm Logout";
        const message = form.getAttribute("data-logout-message") || "Are you sure you want to log out?";

        const titleNode = modalElement.querySelector("#logoutConfirmTitle");
        const messageNode = modalElement.querySelector("#logoutConfirmMessage");
        if (titleNode) {
            titleNode.textContent = title;
        }
        if (messageNode) {
            messageNode.textContent = message;
        }

        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        modal.show();
    });

    document.addEventListener("click", function (event) {
        const button = event.target.closest("#logoutConfirmSubmit");
        if (!button || !pendingLogoutForm) {
            return;
        }

        pendingLogoutForm.dataset.logoutConfirmed = "true";
        pendingLogoutForm.submit();
    });
});
