using System;
using System.Collections.Generic;

// 뉴스 한 건. 기업은 이름 대신 ID로 들고 있다가 화면에 그릴 때 이름으로 바꾼다.
[Serializable]
public class NewsEntry
{
    public int turn;
    public string title;
    public List<string> companyIDs = new List<string>();
}
