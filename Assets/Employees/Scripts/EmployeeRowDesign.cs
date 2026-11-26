using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TakeOver.NPC;
namespace TakeOver.Employees
{
    public class EmployeeRowDesign : MonoBehaviour
    {
        [SerializeField] private Image face, gradeBadge;
        [SerializeField] private Sprite rookie, mid, veteran;
        [SerializeField] private TextMeshProUGUI jobText;
        private static readonly Sprite[,] portraits = new Sprite[2,2];
        public void Refresh(Employee employee)
        {
            if (face != null) face.sprite = Portrait(employee.Appearance, false);
            if (gradeBadge != null) gradeBadge.sprite = employee.Grade == CareerGrade.Rookie ? rookie : employee.Grade == CareerGrade.Mid ? mid : veteran;
            var jobs = FindFirstObjectByType<JobService>();
            var job = jobs != null ? jobs.GetJob(employee) : JobType.None;
            if (jobText != null) jobText.text = job == JobType.None ? "없음" :
                job == JobType.Support ? "지원 인원 배치" :
                (job == JobType.Appraisal ? "시세 예측" : "스타트업 발굴") + " · D-" + jobs.GetRemainingDays(employee);
        }
        public static Sprite Portrait(NpcMobAppearance appearance, bool comparison)
        {
            int gender = appearance == NpcMobAppearance.Female ? 1 : 0, kind = comparison ? 1 : 0;
            if (portraits[gender,kind] != null) return portraits[gender,kind];
            var texture = Resources.Load<Texture2D>("NPC/MobStanding/" + (gender == 1 ? "Mob_Female_Standing" : "Mob_Male_Standing"));
            Rect crop = comparison
                ? gender == 1 ? new Rect(323,1440,300,356) : new Rect(346,1607,253,300)
                : gender == 1 ? new Rect(277,1442,400,400) : new Rect(275,1531,388,388);
            float sx=texture.width/966f, sy=texture.height/1919f;
            crop = new Rect(crop.x*sx,crop.y*sy,crop.width*sx,crop.height*sy);
            return portraits[gender,kind] = Sprite.Create(texture,crop,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        }
    }
}
