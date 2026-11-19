namespace TakeOver.Employees
{
    // 한 직원은 한 번에 직무 하나. Support는 값만 있고 이번 구현에서는 배정하지 않는다.
    public enum JobType
    {
        None,
        Appraisal,
        Discovery,
        Support
    }
}
