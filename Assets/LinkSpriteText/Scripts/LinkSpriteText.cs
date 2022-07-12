using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LinkSpriteText.Scripts
{
    /// <summary>
    /// 文本控件，支持超链接、图片
    /// </summary>
    [AddComponentMenu("UI/LinkImageText", 10)]
    public class LinkSpriteText : Text, IPointerClickHandler, IPointerDownHandler
    {
        [TextArea(3, 10), SerializeField] protected string originText;


        public override string text
        {
            get => _outputText;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    originText = String.Empty;
                    _mTextDirty = true;
                    SetVerticesDirty();
                }
                else
                {
                    if (originText == value)
                    {
                        return;
                    }

                    originText = value;
                    _mTextDirty = true;
                    SetVerticesDirty();
                }
            }
        }

        public string customLinkColor = "blue";

        public void SetLinkColor(string c)
        {
            customLinkColor = c;
            _mTextDirty = false;
        }


        /// <summary>
        /// 解析完最终的文本
        /// </summary>
        private string _outputText;


        /// <summary>
        /// 对应的顶点输出的文本
        /// </summary>
        private string _vertexText;

        /// <summary>
        /// 是否需要解析
        /// </summary>
        private bool _mTextDirty = true;


        /// <summary>
        /// 图片池
        /// </summary>
        private readonly List<Image> _mImagesPool = new List<Image>();

        /// <summary>
        /// 图片的最后一个顶点的索引
        /// </summary>
        private readonly List<int> _mImagesVertexIndex = new List<int>();

        /// <summary>
        /// 超链接信息列表
        /// </summary>
        private readonly List<LinkInfo> _mLinkInfos = new List<LinkInfo>();

        [Serializable]
        public class LinkClickEvent : UnityEvent<string>
        {
        }


        /// <summary>
        /// 超链接点击事件
        /// </summary>
        public LinkClickEvent onLinkClick;


        public LinkClickEvent onLinkDown;


        /// <summary>
        /// 正则取出所需要的属性
        /// </summary>
        private static readonly Regex ImageRegex =
            new Regex(@"<quad name=(.+?) size=(\d*\.?\d+%?) width=(\d*\.?\d+%?) />", RegexOptions.Singleline);

        /// <summary>
        /// 超链接正则
        /// </summary>
        private static readonly Regex LinkRegex =
            new Regex(@"<a link=([^>\n\s]+)>(.*?)(</a>)", RegexOptions.Singleline);

        /// <summary>
        /// 加载精灵图片方法
        /// </summary>
        public static Func<string, Sprite> FunLoadSprite => Resources.Load<Sprite>;


        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();
            _mTextDirty = true;
            UpdateQuadImage();
        }


        private void UpdateQuadImage()
        {
            if (_mTextDirty)
            {
                _outputText = GetOutputText(originText);
            }


            _mImagesVertexIndex.Clear();
            int startSearchIndex = 0;
            var matches = ImageRegex.Matches(originText);
            for (var i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                int index = _vertexText.IndexOf('&', startSearchIndex);

                var firstIndex = index * 4;
                startSearchIndex = index + 1;

                _mImagesVertexIndex.Add(firstIndex);

                _mImagesPool.RemoveAll(image => image == null);
                if (_mImagesPool.Count == 0)
                {
                    GetComponentsInChildren(_mImagesPool);
                }

                if (_mImagesVertexIndex.Count > _mImagesPool.Count)
                {
                    var resources = new DefaultControls.Resources();
                    var go = DefaultControls.CreateImage(resources);
                    go.layer = gameObject.layer;
                    var rt = go.transform as RectTransform;
                    if (rt)
                    {
                        rt.SetParent(rectTransform, false);
                        rt.localPosition = Vector3.zero;
                        rt.localRotation = Quaternion.identity;
                        rt.localScale = Vector3.one;
                    }

                    _mImagesPool.Add(go.GetComponent<Image>());
                }

                var spriteName = match.Groups[1].Value;
                var img = _mImagesPool[i];
                if (img.sprite == null || img.sprite.name != spriteName)
                {
                    img.sprite = FunLoadSprite(spriteName);
                }

                var imgRectTransform = img.GetComponent<RectTransform>();
                if (Int32.TryParse(match.Groups[2].Value, out int size))
                {
                    imgRectTransform.sizeDelta = new Vector2(size, size);
                }
                else
                {
                    Debug.LogWarning("无法正常解析大小");
                    imgRectTransform.sizeDelta = new Vector2(16f, 16f);
                }

                img.enabled = true;
            }

            for (var i = _mImagesVertexIndex.Count; i < _mImagesPool.Count; i++)
            {
                if (_mImagesPool[i])
                {
                    _mImagesPool[i].enabled = false;
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            m_DisableFontTextureRebuiltCallback = true;
            base.OnPopulateMesh(toFill);
            UIVertex vert = new UIVertex();


            for (var i = 0; i < _mImagesVertexIndex.Count; i++)
            {
                var index = _mImagesVertexIndex[i];
                var rt = _mImagesPool[i].rectTransform;
                var size = rt.sizeDelta;
                if (index < toFill.currentVertCount)
                {
                    toFill.PopulateUIVertex(ref vert, index);
                    rt.anchoredPosition = new Vector2(vert.position.x + size.x / 2, vert.position.y - size.y * 0.625f);
                    toFill.PopulateUIVertex(ref vert, index);
                    for (int j = index + 3, m = index; j > m; j--)
                    {
                        toFill.SetUIVertex(vert, j);
                    }
                }
            }


            // 处理超链接包围框
            foreach (var info in _mLinkInfos)
            {
                info.Boxes.Clear();
                if (info.StartIndex >= toFill.currentVertCount)
                {
                    continue;
                }

                // 将超链接里面的文本顶点索引坐标加入到包围框
                toFill.PopulateUIVertex(ref vert, info.StartIndex);
                var pos = vert.position;
                var bounds = new Bounds(pos, Vector3.zero);
                for (int i = info.StartIndex, m = info.EndIndex; i < m; i++)
                {
                    if (i >= toFill.currentVertCount)
                    {
                        break;
                    }

                    toFill.PopulateUIVertex(ref vert, i);
                    pos = vert.position;
                    if (pos.x < bounds.min.x) // 换行重新添加包围框
                    {
                        info.Boxes.Add(new Rect(bounds.min, bounds.size));
                        bounds = new Bounds(pos, Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(pos); // 扩展包围框
                    }
                }

                info.Boxes.Add(new Rect(bounds.min, bounds.size));
            }

            m_DisableFontTextureRebuiltCallback = false;
        }


        /// <summary>
        /// 获取超链接解析后的最后输出文本 
        /// </summary>
        /// <returns></returns>
        protected virtual string GetOutputText(string outputText)
        {
            _mLinkInfos.Clear();
            if (string.IsNullOrEmpty(outputText))
                return "";
            string tempOutputText = outputText;
            _vertexText = outputText;
            _vertexText = Regex.Replace(_vertexText, "<color.*?>", "");
            _vertexText = Regex.Replace(_vertexText, "</color>", "");
            _vertexText = ImageRegex.Replace(_vertexText, "&");
            _vertexText = _vertexText.Replace("\n", "");
            _vertexText = Regex.Replace(_vertexText, @"(?<!a)\s(?!link)", "");
            foreach (Match match in LinkRegex.Matches(_vertexText))
            {
                var group = match.Groups[1];
                _vertexText = _vertexText.Replace(match.Value, match.Groups[2].Value);
                int startNum = _vertexText.IndexOf(match.Groups[2].Value, match.Index, StringComparison.Ordinal);
                if (startNum < 0)
                {
                    Debug.LogError("超链接顶点解析错误");
                }

                var info = new LinkInfo
                {
                    StartIndex = startNum * 4, // 超链接里的文本起始顶点索引

                    EndIndex = startNum * 4 + (match.Groups[2].Length - 1) * 4 + 3,
                    Name = group.Value
                };
                _mLinkInfos.Add(info);
            }


            foreach (Match match in LinkRegex.Matches(outputText))
            {
                tempOutputText = tempOutputText.Replace(match.Value,
                    $"<color={customLinkColor}>" + match.Groups[2].Value + "</color>");
            }


            _mTextDirty = false;

            return tempOutputText;
        }

        #region 回调事件

        /// <summary>
        /// 点击事件检测是否点击到超链接文本
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out var lp);

            foreach (var info in _mLinkInfos)
            {
                var boxes = info.Boxes;
                for (var i = 0; i < boxes.Count; ++i)
                {
                    if (!boxes[i].Contains(lp)) continue;
                    onLinkClick.Invoke(info.Name);
                    return;
                }
            }
        }

        /// <summary>
        /// 点击事件检测是否点击到超链接文本
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out var lp);

            foreach (var info in _mLinkInfos)
            {
                var boxes = info.Boxes;
                for (var i = 0; i < boxes.Count; ++i)
                {
                    if (!boxes[i].Contains(lp)) continue;
                    onLinkDown.Invoke(info.Name);
                    return;
                }
            }
        }

        #endregion

        /// <summary>
        /// 超链接信息类
        /// </summary>
        private class LinkInfo
        {
            public int StartIndex;

            public int EndIndex;

            public string Name;

            public readonly List<Rect> Boxes = new List<Rect>();
        }
    }
}