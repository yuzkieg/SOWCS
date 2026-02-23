1. Main Admin
The primary overseer of the platform responsible for system integrity and user governance.
Key Responsibilities:
User Governance: Managing user accounts and assigning specific roles (Employee, Manager, HR).
System Maintenance: Maintaining core system data and ensuring uptime.
Auditing: Accessing all modules and viewing detailed system logs/reports for security and accountability.
Primary Modules: User Management, Audit Logs.

2. HR Officer
The agent responsible for personnel management and organizational records.
Key Responsibilities:
Record Management: Viewing and managing comprehensive employee staff records.
Leave Administration: Reviewing all leave requests submitted by staff and updating leave balances.
Reporting: Generating basic HR reports regarding department status and employee roles.
Primary Modules: Employees Page, Approvals (HR-specific), HRMS.

3. Manager
The operational agent focused on project execution and workflow approvals.
Key Responsibilities:
Workflow Approval: Reviewing and either approving or rejecting leave requests and official documents.
Project Leadership: Creating new projects and assigning specific tasks to employees.
Oversight: Monitoring real-time project progress and team performance.
Primary Modules: Projects, Approvals (Managerial), Workflow Management.

4. Employee (End-User)
The standard user agent interacting with the system for daily tasks and self-service.
Key Responsibilities:
Self-Service: Submitting personal leave requests and uploading digital documents.
Task Management: Viewing assigned project tasks and updating their completion status.
Collaboration: Accessing shared office documents and tracking their own recent activity.
Primary Modules: My Tasks, Documents, Leave Requests, Dashboard.

## Technology Integration for Agents
Identity Provider: All agents authenticate via Google Sign-In API and OAuth 2.0.
Communication: Email notifications for agent actions (Approvals/Tasks) are handled via Gmail SMTP/API.
Data Retrieval: Employee data for all agents is synchronized through the Google People API.
Would you like me to expand on the specific API permissions required for each of these agents?