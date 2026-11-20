using UnityEngine;

namespace TakeOver.Startups
{
    [CreateAssetMenu(fileName = "StartupDiscoverySettings", menuName = "Company/Startup Discovery Settings")]
    public sealed class StartupDiscoverySettings : ScriptableObject
    {
        [Header("공통")]
        [Tooltip("직원 능력치 최댓값. 이 값일 때 아래 '최대' 값들이 적용된다 (EmployeeConfig.maxStatValue와 같게)")]
        [Min(1)] public int statMax = 20;

        [Tooltip("0이면 실행할 때마다 다른 결과. 0이 아니면 항상 같은 결과 (검증용)")]
        public int randomSeed = 0;

        [Header("후보 수 (발품)")]
        [Tooltip("발품 0일 때 후보 수")]
        [Min(1)] public int candidatesAtZero = 1;
        [Tooltip("발품이 최대일 때 후보 수. 사이는 직선으로 이어 반올림한다")]
        [Min(1)] public int candidatesAtMax = 5;

        [Header("후보의 질 (안목)")]
        [Tooltip("안목이 최대일 때, 성장률 1등 회사가 꼴찌 회사보다 뽑힐 가능성이 e^(이 값)배. 0이면 안목과 상관없이 무작위")]
        [Min(0f)] public float eyeBias = 3f;

        [Header("성장 전망 (분석력)")]
        [Tooltip("숨은 성장률이 이 값 이상이면 실제 전망 '상' (0.065 = 6.5%)")]
        public float highOutlookGrowth = 0.065f;
        [Tooltip("숨은 성장률이 이 값 미만이면 실제 전망 '하'. 사이는 '중'")]
        public float lowOutlookGrowth = 0.025f;
        [Tooltip("분석력 0일 때 전망이 맞을 확률")]
        [Range(0f, 1f)] public float accuracyAtZero = 0.4f;
        [Tooltip("분석력이 최대일 때 전망이 맞을 확률. 사이는 직선")]
        [Range(0f, 1f)] public float accuracyAtMax = 0.95f;

        [Header("후보 소개 횟수")]
        [Tooltip("같은 스타트업이 발굴 보고에 나올 수 있는 최대 횟수. 다 채우면 다른 투자자가 가져간 것으로 보고 다시 나오지 않는다")]
        [Min(1)] public int maxTimesShown = 3;

        [Header("문서")]
        [Tooltip("발굴 보고의 처리 기한 (턴). 기한이 지나면 하루 넘기기가 막힌다")]
        [Min(1)] public int deadlineTurns = 7;
    }
}
