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