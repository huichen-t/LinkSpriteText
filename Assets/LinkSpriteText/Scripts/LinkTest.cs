using System.Collections;
using System.Collections.Generic;
using LinkSpriteText.Scripts;
using UnityEngine;

public class LinkTest : MonoBehaviour
{
    public LinkSpriteText.Scripts.LinkSpriteText _text;

    // Start is called before the first frame update
    void Start()
    {
        _text.onLinkClick.AddListener(OnClick);
    }

    void OnClick(string s)
    {
        Debug.Log("触发超链接点击:" + s);
    }

    // Update is called once per frame
    void Update()
    {
    }
}