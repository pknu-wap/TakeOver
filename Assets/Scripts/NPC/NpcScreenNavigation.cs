using UnityEngine;
using UnityEngine.Events;

namespace TakeOver.NPC
{
    /// <summary>NPC 화면에서 이벤트 및 포트폴리오 파트로 진입하는 공용 연결점이다.</summary>
    public sealed class NpcScreenNavigation : MonoBehaviour
    {
        [SerializeField] private UnityEvent onEventScreenRequested = new UnityEvent();
        [SerializeField] private UnityEvent onPortfolioScreenRequested = new UnityEvent();

        public void RequestEventScreen() => onEventScreenRequested?.Invoke();
        public void RequestPortfolioScreen() => onPortfolioScreenRequested?.Invoke();
    }
}
