namespace TakeOver.NPC
{
    /// <summary>현재 프로필로 재계산하지 않고 기억에 저장된 효과와 분류명을 표시한다.</summary>
    internal static class NpcMemoryPresentation
    {
        /// <summary>현재 프로필로 재계산하지 않고 마지막 Memory에 저장된 보정 근거를 표시한다.</summary>
        internal static string FormatLastMemoryLog(NpcMemoryRecord memory)
        {
            if (memory == null) return "마지막 사건 없음";
            var header = $"마지막 사건: {memory.actionId ?? memory.memoryType.ToString()} / {GetMemoryLabel(memory.memoryType)} · {memory.turn}일";
            var log = memory.effectLog;
            if (log == null) return header + "\n선호 보정 상세 기록 없음 (기존 기록)";
            var status = !log.preferenceEnabled ? "보정 비활성" : memory.actionDisposition == NpcActionDisposition.Unspecified
                ? "미지정 · 기본 효과" : $"{GetActionDispositionLabel(memory.actionDisposition)} ×{log.multiplier:0.##}";
            if (log.usedOverride) status = "개별 설정 · " + status;
            return header + $" · {status}\n기본(프로필 보정 포함): {FormatDelta(log.beforePreference)}"
                + $"\n선호·불호 추가분: {FormatDelta(log.preferenceBonus)}"
                + (log.usedOverride ? $"\n개별 고정 보너스: {FormatDelta(log.configuredBonus)}" : "")
                + $"\n최종 요청: {FormatDelta(memory.relationDelta)} / 실제 반영: {FormatDelta(log.actuallyApplied)}"
                + $"\n기록 ID: {memory.memoryId}";
        }

        private static string FormatDelta(NpcRelationDelta delta)
        {
            var parts = new System.Collections.Generic.List<string>();
            var names = new[] { "신뢰", "존중", "두려움", "적대감", "의존도", "관심" };
            var values = new[] { delta.trust, delta.respect, delta.fear, delta.hostility, delta.dependency, delta.interest };
            for (var i = 0; i < names.Length; i++)
                if (values[i] != 0) parts.Add($"{names[i]} {values[i]:+0.##;-0.##;0}");
            return parts.Count == 0 ? "변화 없음" : string.Join(" · ", parts);
        }

        /// <summary>테스트 화면에서 enum 이름 대신 팀원이 읽기 쉬운 기억 이름을 보여준다.</summary>
        internal static string GetActionDispositionLabel(NpcActionDisposition disposition)
        {
            switch (disposition)
            {
                case NpcActionDisposition.Likes: return "선호";
                case NpcActionDisposition.Dislikes: return "불호";
                default: return "미지정";
            }
        }

        internal static string GetMemoryLabel(NpcMemoryType memoryType)
        {
            switch (memoryType)
            {
                case NpcMemoryType.GoodFaith: return "선의 / 약속 이행";
                case NpcMemoryType.ContractBreach: return "계약 파기";
                case NpcMemoryType.MassLayoff: return "대규모 구조조정";
                case NpcMemoryType.HostileTakeover: return "적대적 인수";
                case NpcMemoryType.SavedCompany: return "회사 구원";
                case NpcMemoryType.PublicHumiliation: return "공개적 망신";
                case NpcMemoryType.LeakedInfo: return "정보 유출";
                case NpcMemoryType.Bribery: return "뇌물";
                default: return "기타";
            }
        }

    }
}
