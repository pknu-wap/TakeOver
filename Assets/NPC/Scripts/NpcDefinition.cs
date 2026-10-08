using UnityEngine;

namespace TakeOver.NPC
{
    /// <summary>팀원이 Inspector에서 편집하는 NPC 정적 양식. 관계·기억·저장 상태는 에셋에 쓰지 않는다.</summary>
    [CreateAssetMenu(fileName = "NewNpc", menuName = "TakeOver/NPC/NPC Definition")]
    public sealed class NpcDefinition : ScriptableObject
    {
        [SerializeField] private NpcStateRegistry.NpcSeed seed = new NpcStateRegistry.NpcSeed();

        /// <summary>프로필과 선호 목록까지 독립 사본으로 복사해 런타임 변경이 원본에 전파되지 않게 한다.</summary>
        public NpcStateRegistry.NpcSeed CreateRuntimeSeed() =>
            JsonUtility.FromJson<NpcStateRegistry.NpcSeed>(JsonUtility.ToJson(seed));
    }
}
