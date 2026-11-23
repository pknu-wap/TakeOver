namespace TakeOver.Employees
{
    // 한 직원은 한 번에 직무 하나. Support는 인원 요청 승인이나 투자 계약서로 대상 회사에 배치한다.
    public enum JobType
    {
        None,
        Appraisal,
        Discovery,
        Support
    }
}
