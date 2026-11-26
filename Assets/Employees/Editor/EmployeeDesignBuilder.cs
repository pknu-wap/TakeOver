#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace TakeOver.Employees.Editor
{
    public static class EmployeeDesignBuilder
    {
        public static readonly Color White = new Color32(246,246,246,255);
        public static readonly Color Gray = new Color32(186,186,186,255);
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1);
            r.anchoredPosition = new Vector2(x,-y);
            r.sizeDelta = new Vector2(w,h);
            return r;
        }
        public static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var r = Rect(parent,name,x,y,w,h);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ShareTrading/Fonts/Pretendard-Bold SDF.asset");
            t.text = value; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
        public static Image Image(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
        {
            var i = Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            i.sprite = sprite; i.color = color; i.type = UnityEngine.UI.Image.Type.Sliced; i.raycastTarget = false;
            return i;
        }
        public static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Employees/Art/"+name+".png");
        public static void Assign(Object owner, string field, Object value)
        {
            var so = new SerializedObject(owner);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Clear(Transform parent)
        {
            while(parent.childCount>0) Object.DestroyImmediate(parent.GetChild(0).gameObject);
        }
        public static Sprite Surface(string name, int width, int height, float radius, Color top, Color bottom, float stroke=0, Color border=default(Color), int glow=0, bool dashed=false, bool shadow=false, float glowOpacity=.6f, Color? sheen=null)
        {
            int w=width+glow*2, h=height+glow*2;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Vector2 q=new Vector2(Mathf.Abs(x+.5f-w*.5f)-(width*.5f-radius),Mathf.Abs(y+.5f-h*.5f)-(height*.5f-radius));
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                Color c=Color.clear;
                if(d<=0)
                {
                    c=Color.Lerp(bottom,top,Mathf.Clamp01((y-glow)/(float)height));
                    if (sheen.HasValue) c=Color.Lerp(c,sheen.Value,Mathf.Clamp01(8-Mathf.Abs(x+.5f+(h-y-.5f)*.6f-51)));
                    if(stroke>0 && d>-stroke && (!dashed || ((int)(Mathf.Abs(x+.5f-w*.5f)>width*.5f-radius?y:x)/15)%2==0))c=border;
                    c.a*=Mathf.Clamp01(.5f-d);
                }
                else if(glow>0)c=shadow?new Color(0,0,0,.5f*Mathf.Exp(-d*d/(glow*glow*.5f))):new Color(92/255f,195/255f,1,glowOpacity*Mathf.Exp(-d*d/(glow*glow*.5f)));
                pixels[y*w+x]=c;
            }
            texture.SetPixels(pixels);texture.Apply();
            string path="Assets/Employees/Art/"+name+".png";
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spriteBorder=new Vector4(radius+glow,radius+glow,radius+glow,radius+glow);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Button Button(Transform parent, string name, string caption, Sprite sprite, float x, float y, float w, float h, int size, Color textColor)
        {
            var image=Image(parent,name,sprite,Color.white,x,y,w,h); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
            Text(image.transform,"Label",caption,0,0,w,h,size,textColor,TextAlignmentOptions.Center);
            return button;
        }
        public static AbilityRadarGraphic Radar(Transform parent, string name, float x, float y)
        {
            var rect=Rect(parent,name,x,y,236.41f,236.41f);
            var radar=rect.gameObject.AddComponent<AbilityRadarGraphic>();radar.raycastTarget=false;
            Vector2[] centers={new Vector2(118.205f,-26),new Vector2(281.205f,78),new Vector2(209.205f,236),new Vector2(12.205f,229),new Vector2(-50.795f,76)};
            var labels=new TextMeshProUGUI[5];
            for(int i=0;i<5;i++)labels[i]=Text(rect,"Axis"+i,"",centers[i].x-70,centers[i].y-18,140,36,20,new Color32(96,96,96,255),TextAlignmentOptions.Center);
            radar.SetLabels(labels);return radar;
        }
    }
}
#endif
