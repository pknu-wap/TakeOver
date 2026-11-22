namespace TakeOver.Employees
{
    // 한 직원은 한 번에 직무 하나. Support는 스타트업 한 곳에 묶이는 지원 인원 배치이며 업무 주기 없이 계속 유지된다.
    public enum JobType
    {
        None,
        Appraisal,
        Discovery,
        Support
    }
}
