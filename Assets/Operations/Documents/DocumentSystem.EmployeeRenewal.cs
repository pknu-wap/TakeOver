using System.Collections.Generic;
using System.Linq;
using TakeOver.Employees;
using TakeOver.SaveSystem;

namespace TakeOver.Documents
{
    public sealed partial class DocumentSystem
    {
        private EmployeeService renewalEmployees;
        private readonly List<EmployeeRenewalDocument> cancelledRenewals = new List<EmployeeRenewalDocument>();

        private void OnEnable()
        {
            if (!TryGetComponent(out renewalEmployees)) return;
            renewalEmployees.OnContractRenewalNotice += issueEmployeeRenewal;
            renewalEmployees.OnContractExpired += rejectExpiredEmployeeRenewal;
            renewalEmployees.OnEmployeeLeft += cancelEmployeeRenewal;
        }

        private void OnDisable()
        {
            if (renewalEmployees == null) return;
            renewalEmployees.OnContractRenewalNotice -= issueEmployeeRenewal;
            renewalEmployees.OnContractExpired -= rejectExpiredEmployeeRenewal;
            renewalEmployees.OnEmployeeLeft -= cancelEmployeeRenewal;
        }

        private void issueEmployeeRenewal(Employee employee) =>
            Inbox.Add(new EmployeeRenewalDocument(employee, renewalEmployees, clock));

        private void rejectExpiredEmployeeRenewal(Employee employee)
        {
            EmployeeRenewalDocument document = findEmployeeRenewal(employee);
            if (document == null) return;

            Inbox.Resolve(document, false);
            if (document.WasAutomaticallyRejected) renewalEmployees.Dismiss(employee);
        }

        private void cancelEmployeeRenewal(Employee employee)
        {
            EmployeeRenewalDocument document = findEmployeeRenewal(employee);
            if (document == null) return;

            if (GameSessionCoordinator.Active?.IsPersistenceBlocked == true)
                cancelledRenewals.Add(document);
            else
                Inbox.Resolve(document, false);
        }

        private void Update()
        {
            if (cancelledRenewals.Count == 0 || GameSessionCoordinator.Active?.IsPersistenceBlocked == true) return;
            foreach (EmployeeRenewalDocument document in cancelledRenewals) Inbox.Resolve(document, false);
            cancelledRenewals.Clear();
        }

        private EmployeeRenewalDocument findEmployeeRenewal(Employee employee) =>
            Inbox.Open.OfType<EmployeeRenewalDocument>().FirstOrDefault(document => document.IsFor(employee));
    }
}
