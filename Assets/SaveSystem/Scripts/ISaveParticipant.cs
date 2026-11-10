namespace TakeOver.SaveSystem
{
    /// <summary>기본 파트의 파생 수치가 완성된 뒤 화면을 재구성할 때 사용</summary>
    public interface ISaveRebuildOrder
    {
        int rebuildOrder { get; }
    }
    /// <summary>각 파트의 데이터 형식과 복원 방법을 연결하는 저장 계약</summary>
    public interface ISaveParticipant
    {
        string saveId { get; }
        int saveVersion { get; }

        /// <summary>게임 상태 변경 없이 독립적인 JSON 사본 반환</summary>
        string captureJson();

        /// <summary>해석·버전 변환·검증만 수행. 실패 시 예외 발생, 실제 상태 유지</summary>
        void prepareLoad(string json, int savedVersion, DayStartData dayStart);

        /// <summary>준비된 값 적용. 턴 진행·보상 지급·변경 알림 제외</summary>
        void applyLoad();

        /// <summary>전체 적용 후 참조·파생 수치 재구성. 난수·날짜 진행 제외</summary>
        void rebuildAfterLoad();
    }
}
