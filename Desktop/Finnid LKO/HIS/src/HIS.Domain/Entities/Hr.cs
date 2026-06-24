namespace HIS.Domain.Entities;

/// <summary>Staff master — SRS §3.17.</summary>
public sealed class Staff
{
    public long StaffId { get; set; }
    public int BranchId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Daily attendance — SRS §3.17. One row per staff per day.</summary>
public sealed class Attendance
{
    public long AttendanceId { get; set; }
    public long StaffId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan? InTime { get; set; }
    public TimeSpan? OutTime { get; set; }
    public string Status { get; set; } = "Present";   // Present/Absent/Leave/Half-day
}

/// <summary>Leave request — SRS §3.17.</summary>
public sealed class LeaveRequest
{
    public long LeaveId { get; set; }
    public long StaffId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? LeaveType { get; set; }
    public string Status { get; set; } = "Pending";    // Pending/Approved/Rejected
}

/// <summary>Monthly payroll run incl. overtime — SRS §3.18.</summary>
public sealed class PayrollRun
{
    public long PayrollId { get; set; }
    public long StaffId { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal BasicPay { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
    public long? OvertimeApprovedBy { get; set; }
    public string Status { get; set; } = "Draft";       // Draft/Approved/Paid
}
