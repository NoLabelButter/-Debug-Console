using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DebugConsole
{
    /// <summary>一条悬停记录：按钮的矩形 + 底色，鼠标压上去时自动提亮。</summary>
    internal sealed class UiHover
    {
        public RectTransform Rect;
        public Image Bg;
        public Color Base;
    }

    /// <summary>一个自建按钮的句柄，方便后面改文字、改底色、显隐。</summary>
    internal sealed class UiButton
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Bg;
        public Button Btn;
        public TextMeshProUGUI Label;
        public UiHover Hover;
        public float X, Y, W, H;

        /// <summary>标签的中文原文（还没翻、没截）。</summary>
        public string Raw;
        public float FontPx;
        /// <summary>左边距：左对齐的按钮文字不从边上看齐。</summary>
        public float PadX;

        public void Place(float x, float y, float w, float h)
        {
            X = x; Y = y; W = w; H = h;
            Ui.Place(Go, x, y, w, h);
            if (!string.IsNullOrEmpty(Raw)) Fit();
        }

        /// <summary>
        /// 按实际宽度把标签截到框里。英文比中文长将近一倍，
        /// 不截的话「Raise the gun for 3 s」这种会顶出按钮边框。
        /// </summary>
        public void Fit()
        {
            try
            {
                if (Label == null) return;
                string s = L10n.S(Raw ?? "");
                if (W > 8f) s = Ui.Cut(s, FontPx > 0f ? FontPx : 13f, W - PadX * 2f - 4f);
                Label.text = Palette.Rt(s);
            }
            catch { }
        }

        public void SetBg(Color c)
        {
            try
            {
                if (Bg != null) Bg.color = c;
                if (Hover != null) Hover.Base = c;
            }
            catch { }
        }

        public void SetText(string text)
        {
            Raw = text;
            Fit();
        }

        public void SetColor(Color c)
        {
            try { if (Label != null) Label.color = c; } catch { }
        }

        public void SetLabel(string text, Color c)
        {
            SetText(text);
            SetColor(c);
        }

        public void SetActive(bool on)
        {
            Ui.SetActive(Go, on);
        }
    }

    /// <summary>
    /// 自建 UGUI 工具集。API 手法照搬 PSPDA / StockMarket：
    /// 一律用 TypeOf&lt;T&gt; + AddComponent，绝不用 new GameObject(name, typeof(T))。
    /// 布局基础是 Place()：锚点左上、轴心左上，坐标就是「从父容器左上角量」。
    /// </summary>
    internal static class Ui
    {
        // TMP 对齐枚举用数值，避免枚举名解析问题。
        public const int AlignCenter = 514;
        public const int AlignLeft = 513;
        public const int AlignRight = 516;

        private static Sprite _white;
        private static Sprite _card;   // 大圆角（面板、卡片）
        private static Sprite _chip;   // 小圆角（按钮、槽）

        // ── 基础 ──────────────────────────────────────────────────────

        public static Sprite White()
        {
            if (_white == null)
            {
                _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            }
            return _white;
        }

        /// <summary>运行时生成的圆角九宫格图（卡片用，圆角 12px）。</summary>
        public static Sprite Card()
        {
            if (_card == null) _card = Round(40, 12, 13f);
            return _card;
        }

        /// <summary>运行时生成的圆角九宫格图（按钮/槽用，圆角 8px）。</summary>
        public static Sprite Chip()
        {
            if (_chip == null) _chip = Round(24, 8, 9f);
            return _chip;
        }

        /// <summary>运行时画一张圆角白图，交给九宫格拉伸。失败返回 null，调用方会自动退回纯色块。</summary>
        private static Sprite Round(int size, int radius, float border)
        {
            try
            {
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, CornerAlpha(x, y, size, radius)));
                    }
                }
                tex.Apply();
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0u, 0, new Vector4(border, border, border, border));
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[界面] 生成圆角图失败，改用直角：" + ex.Message);
                return null;
            }
        }

        /// <summary>像素到圆角矩形边界的距离，用来做 1px 抗锯齿。</summary>
        private static float CornerAlpha(int x, int y, int size, int r)
        {
            float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
            float cy = Mathf.Min(y + 0.5f, size - y - 0.5f);
            if (cx >= r || cy >= r) return 1f;
            float dx = r - cx;
            float dy = r - cy;
            float d = r - Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(d + 0.5f);
        }

        /// <summary>返回 Unity Mono 运行时类型。</summary>
        public static Type TypeOf<T>() where T : UnityEngine.Object { return typeof(T); }

        public static T Add<T>(GameObject go, string what) where T : Component
        {
            try
            {
                return go.AddComponent<T>();
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[界面] 挂 " + what + " 失败：" + ex.Message);
                return default(T);
            }
        }

        /// <summary>建一个带 RectTransform 的空物件。</summary>
        public static GameObject New(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            RectTransform rt = Add<RectTransform>(go, name + ".Rect");
            if (rt == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            if (parent != null) rt.SetParent(parent, false);
            return go;
        }

        public static RectTransform Rect(GameObject go)
        {
            try { return go == null ? null : go.transform as RectTransform; }
            catch { return null; }
        }

        // ── 布局 ──────────────────────────────────────────────────────

        /// <summary>铺满父容器，四周留 pad。</summary>
        public static void Stretch(GameObject go, float pad)
        {
            RectTransform rt = Rect(go);
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>铺满父容器，四边各留不同的边距（描边套纸面用）。</summary>
        public static void Inset(GameObject go, float left, float bottom, float right, float top)
        {
            RectTransform rt = Rect(go);
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>相对父容器左上角定位。整个界面的布局基础。</summary>
        public static void Place(GameObject go, float x, float y, float w, float h)
        {
            RectTransform rt = Rect(go);
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localRotation = Quaternion.identity;
        }

        /// <summary>以父容器中心为原点定位（画布根节点用）。</summary>
        public static void PlaceCentered(GameObject go, float dx, float dy, float w, float h)
        {
            RectTransform rt = Rect(go);
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(dx, dy);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void SetActive(GameObject go, bool on)
        {
            try { if (go != null && go.activeSelf != on) go.SetActive(on); } catch { }
        }

        /// <summary>
        /// 关掉 UGUI 的方向键 / Tab 导航。
        /// 控制台自己要用 Tab 补全、↑↓ 翻历史，让 EventSystem 再去挪焦点就会打架
        /// （表现是「按 Tab 补全完，输入框失焦了」）。
        /// </summary>
        public static void NoNav(Selectable s)
        {
            if (s == null) return;
            try
            {
                Navigation n = s.navigation;
                n.mode = (Navigation.Mode)0;   // 0 = None
                s.navigation = n;
            }
            catch (Exception ex)
            {
                Core.Debug("[界面] 关闭导航失败：" + ex.Message);
            }
        }

        // ── 元件 ──────────────────────────────────────────────────────

        /// <summary>纯色块。</summary>
        public static Image MakeImage(Transform parent, string name, Color color, bool raycast)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;
            Image img = Add<Image>(go, name + ".Image");
            if (img == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            img.sprite = White();
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>
        /// 图片框（物品图标用）。运行时才知道贴哪张图，所以 sprite 留空由调用方塞；
        /// preserveAspect 开着，图标不会被拉成方的。
        /// </summary>
        public static Image MakeSprite(Transform parent, string name, bool raycast)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;
            Image img = Add<Image>(go, name + ".Image");
            if (img == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = raycast;
            try { img.preserveAspect = true; } catch { }
            return img;
        }

        /// <summary>九宫格圆角块。sprite 为空时自动退回纯色块。</summary>
        public static Image MakeSliced(Transform parent, string name, Sprite sprite, Color tint, bool raycast)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;
            Image img = Add<Image>(go, name + ".Image");
            if (img == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            img.sprite = sprite != null ? sprite : White();
            try { img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple; } catch { }
            img.color = tint;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>
        /// 一张卡片：先画描边色圆角块，再往里缩 2px 画纸面。
        /// 返回纸面 Image，需要改底色时用它。
        /// </summary>
        public static Image MakeCard(Transform parent, string name, float x, float y, float w, float h)
        {
            Image rim = MakeSliced(parent, name + ".Rim", Card(), Palette.CardRim, true);
            if (rim != null) Place(rim.gameObject, x, y, w, h);
            Image face = MakeSliced(parent, name + ".Face", Card(), Palette.CardBg, true);
            if (face != null) Place(face.gameObject, x + 2f, y + 2f, w - 4f, h - 4f);
            return face;
        }

        /// <summary>卡内嵌槽（日志区、命令条）。</summary>
        public static Image MakeSlot(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            Image img = MakeSliced(parent, name, Chip(), color, false);
            if (img != null) Place(img.gameObject, x, y, w, h);
            return img;
        }

        /// <summary>一条 1px 分隔线。</summary>
        public static void MakeLine(Transform parent, string name, float x, float y, float w, Color color)
        {
            Image img = MakeImage(parent, name, color, false);
            if (img != null) Place(img.gameObject, x, y, w, 1f);
        }

        public static TextMeshProUGUI MakeText(Transform parent, string name, string text, float fontPx,
            Color color, int align, bool wrap)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;
            TextMeshProUGUI t = Add<TextMeshProUGUI>(go, name + ".TMP");
            if (t == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            TMP_FontAsset font = Fonts.Font();
            if (font != null) t.font = font;
            t.text = Palette.Rt(L10n.S(text ?? ""));
            t.fontSize = fontPx;
            t.color = color;
            try { t.alignment = (TextAlignmentOptions)align; } catch { }
            t.raycastTarget = false;
            try { t.enableWordWrapping = wrap; } catch { }
            try { t.overflowMode = 0; } catch { } // 0 = Overflow，不省略
            try { t.richText = true; } catch { }
            return t;
        }

        /// <summary>
        /// 量一段已经渲染出来的 TMP 文字有多宽（像素）。量不出来就按
        /// 「半角 1.26 单位 / 汉字 2 单位」估一个，量级够用。
        /// </summary>
        public static float TextW(TextMeshProUGUI t)
        {
            if (t == null) return 0f;
            try
            {
                t.ForceMeshUpdate();
                if (t.preferredWidth > 1f) return t.preferredWidth;
            }
            catch { }
            try
            {
                string s = t.text ?? "";
                float units = 0f;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '<') { int e = s.IndexOf('>', i); if (e > i) { i = e; continue; } }
                    units += c < 128 ? 1.26f : 2f;
                }
                return units * t.fontSize * 0.5f;
            }
            catch { }
            return 0f;
        }

        /// <summary>
        /// 按「半角宽」把一句话截短到能放进 maxW 像素，超出补「…」。
        /// 汉字算 2 个半角、ASCII 算 1 个。富文本标签不计宽度；
        /// 截断点若落在标签中间，会先退到标签前面。
        /// </summary>
        public static string Cut(string s, float fontPx, float maxW)
        {
            if (string.IsNullOrEmpty(s) || fontPx <= 0f) return s ?? "";
            int units = (int)(maxW / (fontPx * 0.5f));
            if (units < 6) units = 6;
            int col = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int e = s.IndexOf('>', i);
                    if (e > i) { i = e; continue; }
                }
                int w = c < 128 ? 1 : 2;
                if (col + w > units - 2) return s.Substring(0, i) + "…";
                col += w;
            }
            return s;
        }

        // ── 按钮 ──────────────────────────────────────────────────────

        /// <summary>
        /// 带悬停高亮和点击的按钮。sprite 传 null 就是默认的圆角片。
        /// </summary>
        public static UiButton MakeButton(Transform parent, string name, string label, float fontPx,
            Color bg, Color fg, Action onClick, int align, Color? hoverBase = null)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;

            Image img = Add<Image>(go, name + ".Image");
            if (img == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            img.sprite = Chip();
            try { img.type = Image.Type.Sliced; } catch { }
            img.color = bg;
            img.raycastTarget = true;

            UiButton h = new UiButton
            {
                Go = go, Rect = Rect(go), Bg = img,
                Raw = label, FontPx = fontPx,
                PadX = align == AlignLeft ? 14f : 0f,
            };
            h.Hover = RegisterHover(h.Rect, img, bg);

            TextMeshProUGUI t = MakeText(go.transform, "Label", label, fontPx, fg, align, false);
            if (t != null)
            {
                float padX = h.PadX;
                RectTransform tr = Rect(t.gameObject);
                if (tr != null)
                {
                    tr.anchorMin = Vector2.zero;
                    tr.anchorMax = Vector2.one;
                    tr.pivot = new Vector2(0.5f, 0.5f);
                    tr.offsetMin = new Vector2(padX, 0f);
                    tr.offsetMax = new Vector2(-padX, 0f);
                }
                h.Label = t;
            }

            Button b = Add<Button>(go, name + ".Button");
            if (b != null)
            {
                b.targetGraphic = img;
                try
                {
                    // 悬停高亮自己算（见 UpdateHover），不用 ColorTint：
                    // Mono 版 Unity 的 ColorBlock 可直接配置；这里关闭默认色彩过渡，使用自绘悬停效果。
                    b.transition = Selectable.Transition.None;
                }
                catch (Exception ex)
                {
                    Core.Debug("[界面] 关闭按钮过渡失败：" + ex.Message);
                }
                if (onClick != null)
                {
                    try
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(new UnityAction(() => onClick()));
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Warning("[界面] 绑定按钮失败：" + ex.Message);
                    }
                }
                h.Btn = b;
                NoNav(b);
            }
            return h;
        }

        /// <summary>
        /// 单行输入框（TMP_InputField）。TMP 要求 viewport + textComponent + placeholder 三件套齐备，
        /// 少任何一个都会变成「点了没反应」或者「能打字但看不见字」。
        /// </summary>
        public static TMP_InputField MakeInput(Transform parent, string name, string hint, float fontPx,
            Color bg, Color fg, Color hintColor, Action<string> onSubmit)
        {
            GameObject go = New(name, parent);
            if (go == null) return null;

            Image img = Add<Image>(go, name + ".Image");
            if (img == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            img.sprite = Chip();
            try { img.type = Image.Type.Sliced; } catch { }
            img.color = bg;
            img.raycastTarget = true;

            // viewport 负责裁掉溢出的文字。只留左右内边距、上下各缩 1px：
            // 四边各缩 10px 会把字裁掉一半，实机表现就是「字只剩上半截」。
            GameObject vp = New(name + ".Viewport", go.transform);
            if (vp != null)
            {
                Add<RectMask2D>(vp, name + ".Mask");
                RectTransform vpr = Rect(vp);
                if (vpr != null)
                {
                    vpr.anchorMin = Vector2.zero;
                    vpr.anchorMax = Vector2.one;
                    vpr.pivot = new Vector2(0.5f, 0.5f);
                    vpr.offsetMin = new Vector2(9f, 1f);
                    vpr.offsetMax = new Vector2(-9f, -1f);
                }
            }
            Transform textParent = vp != null ? vp.transform : go.transform;
            TextMeshProUGUI txt = MakeText(textParent, name + ".Text", "", fontPx, fg, AlignLeft, false);
            TextMeshProUGUI ph = MakeText(textParent, name + ".Hint", hint, fontPx, hintColor, AlignLeft, false);
            // 正文与占位符都要铺满 viewport：TMP 按矩形算对齐和换行，
            // 留成 0×0 的话字会贴在左上角、被遮罩裁掉，看着就是「打了字但不显示」
            if (txt != null) Stretch(txt.gameObject, 0f);
            if (ph != null) Stretch(ph.gameObject, 0f);

            TMP_InputField f = Add<TMP_InputField>(go, name + ".Input");
            if (f == null) return null;
            NoNav(f);
            try
            {
                if (vp != null) f.textViewport = Rect(vp);
                if (txt != null) f.textComponent = txt;
                if (ph != null) f.placeholder = ph;
                f.targetGraphic = img;
                f.richText = false;
                try { f.lineType = TMP_InputField.LineType.SingleLine; } catch { }
                try { f.characterLimit = 200; } catch { }
                TMP_FontAsset font = Fonts.Font();
                if (font != null) f.fontAsset = font;
                if (onSubmit != null)
                {
                    f.onSubmit.RemoveAllListeners();
                    f.onSubmit.AddListener(new UnityAction<string>(value => onSubmit(value)));
                }
                f.ForceLabelUpdate();
            }
            catch (Exception ex)
            {
                Core.Log.Warning("[界面] 配置输入框失败：" + ex.Message);
            }
            return f;
        }

        /// <summary>把输入框的当前内容读出来（Il2Cpp 里取 text 可能抛）。</summary>
        public static string TextOf(TMP_InputField f, string fallback = "")
        {
            if (f == null) return fallback;
            try { return f.text ?? fallback; } catch { return fallback; }
        }

        public static void SetTextOf(TMP_InputField f, string s)
        {
            if (f == null) return;
            try
            {
                f.text = s ?? "";
                f.ForceLabelUpdate();
            }
            catch { }
        }

        public static bool IsFocused(TMP_InputField f)
        {
            if (f == null) return false;
            try { return f.isFocused; } catch { return false; }
        }

        public static void Focus(TMP_InputField f)
        {
            if (f == null) return;
            try
            {
                f.Select();
                f.ActivateInputField();
            }
            catch { }
        }

        public static void Blur(TMP_InputField f)
        {
            if (f == null) return;
            try { f.DeactivateInputField(); } catch { }
        }

        // ── 悬停高亮 ──────────────────────────────────────────────────

        private static readonly List<UiHover> _hovers = new List<UiHover>();
        private static int _hoverIndex = -1;

        private static UiHover RegisterHover(RectTransform rect, Image bg, Color baseColor)
        {
            UiHover h = new UiHover { Rect = rect, Bg = bg, Base = baseColor };
            _hovers.Add(h);
            return h;
        }

        /// <summary>重建界面前清空悬停表。</summary>
        public static void ResetHovers()
        {
            _hovers.Clear();
            _hoverIndex = -1;
        }

        /// <summary>
        /// 丢掉已经销毁的悬停记录。物品网格每翻一页都要重铺几十个按钮，
        /// 记录只增不减的话，每帧的悬停检测就会去扫一堆死物件。
        /// </summary>
        public static void PruneHovers()
        {
            for (int i = _hovers.Count - 1; i >= 0; i--)
            {
                UiHover h = _hovers[i];
                if (h == null || h.Rect == null) _hovers.RemoveAt(i);
            }
            _hoverIndex = -1;
        }

        /// <summary>
        /// 每帧调一次：鼠标压在哪个按钮上就把它提亮。
        /// 不用 UGUI 的悬停事件，因为这里直接用轻量的鼠标位置检测。
        /// </summary>
        public static void UpdateHover()
        {
            try
            {
                int found = -1;
                if (Input.mousePresent)
                {
                    Vector2 mouse = Input.mousePosition;
                    for (int i = 0; i < _hovers.Count; i++)
                    {
                        UiHover h = _hovers[i];
                        if (h.Rect == null) continue;
                        if (!h.Rect.gameObject.activeInHierarchy) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(h.Rect, mouse, null))
                        {
                            found = i;
                            break;
                        }
                    }
                }
                if (found == _hoverIndex) return;

                if (_hoverIndex >= 0 && _hoverIndex < _hovers.Count)
                {
                    Paint(_hovers[_hoverIndex], false);
                }
                _hoverIndex = found;
                if (_hoverIndex >= 0) Paint(_hovers[_hoverIndex], true);
            }
            catch (Exception ex)
            {
                Core.Debug("[界面] 悬停检测失败：" + ex.Message);
            }
        }

        private static void Paint(UiHover h, bool on)
        {
            try
            {
                if (h == null || h.Bg == null) return;
                h.Bg.color = on ? Palette.Shift(h.Base, 0.22f) : h.Base;
            }
            catch { }
        }

        // ── 画布 ──────────────────────────────────────────────────────

        /// <summary>建一张独立画布（覆盖模式、最高排序、像素级缩放）。</summary>
        public static Canvas NewCanvas(string name, int order, float scaleFactor = 1f)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = Add<Canvas>(go, name + ".Canvas");
            if (canvas == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }
            try
            {
                canvas.renderMode = 0;      // 0 = ScreenSpaceOverlay
                canvas.sortingOrder = order;
                canvas.overrideSorting = true;
            }
            catch (Exception ex)
            {
                Core.Debug("[界面] 设置画布失败：" + ex.Message);
            }
            CanvasScaler scaler = Add<CanvasScaler>(go, name + ".Scaler");
            if (scaler != null)
            {
                try
                {
                    scaler.uiScaleMode = 0;   // 0 = ConstantPixelSize
                    scaler.scaleFactor = Mathf.Clamp(scaleFactor, 0.75f, 2f);
                    scaler.referencePixelsPerUnit = 100f;
                }
                catch { }
            }
            GraphicRaycaster raycaster = Add<GraphicRaycaster>(go, name + ".Raycaster");
            if (raycaster == null) Core.Log.Warning("[界面] 没挂上 GraphicRaycaster，按钮会点不动。");
            return canvas;
        }

        public static bool Alive(UnityEngine.Object o)
        {
            try { return o != null; } catch { return false; }
        }

        public static void Kill(UnityEngine.Object o)
        {
            try { if (o != null) UnityEngine.Object.Destroy(o); } catch { }
        }
    }

    /// <summary>
    /// 自实现的滚动容器：RectMask2D 视口 + 内容位移 + 滚轮，右侧带一条细滚动条。
    ///
    /// 不用 UGUI 的 ScrollRect，是因为它的滚动位置由原生滚动组件处理
    /// （content/verticalNormalizedPosition 全是只读计算属性），反而比自己算更绕。
    /// </summary>
    internal sealed class UiScroll
    {
        public GameObject View;
        public RectTransform ViewRect;
        public RectTransform Content;    // 内容的父节点，滚动就是挪它
        public Image Bar;                // 滚动条滑块（内容没超出一屏就藏起来）
        public RectTransform BarRect;

        public float X, Y, W, H;
        public float ViewW, ViewH;
        public float ContentH;
        public float Offset;             // 0 = 最顶；越大 = 往下滚得越远（内容整体上移）
        public bool Stick = true;        // 贴底（新内容自动滚到底）

        private const float BarW = 6f;

        public float MaxOffset
        {
            get { return Mathf.Max(0f, ContentH - ViewH); }
        }

        public void Build(Transform parent, string name, float x, float y, float w, float h)
        {
            X = x; Y = y; W = w; H = h;
            ViewW = w - BarW - 6f;   // 右侧留出滚动条的位置
            ViewH = h;

            View = Ui.New(name, parent);
            if (View == null) return;
            Ui.Place(View, x, y, w, h);
            Ui.Add<RectMask2D>(View, name + ".Mask");   // 圆角裁切靠它
            ViewRect = Ui.Rect(View);

            GameObject cgo = Ui.New(name + ".Content", View.transform);
            if (cgo != null)
            {
                Ui.Place(cgo, 0f, 0f, ViewW, 0f);
                Content = Ui.Rect(cgo);
            }

            Bar = Ui.MakeSliced(parent, name + ".Bar", Ui.Chip(), Palette.A(Palette.Muted, 0.55f), false);
            if (Bar != null)
            {
                Ui.Place(Bar.gameObject, x + w - BarW, y, BarW, h);
                BarRect = Ui.Rect(Bar.gameObject);
            }
        }

        public void SetContentHeight(float h)
        {
            ContentH = h < 0f ? 0f : h;
            if (Content != null)
            {
                try { Content.sizeDelta = new Vector2(ViewW, ContentH); } catch { }
            }
        }

        /// <summary>内容变高了：如果原来贴着底，就继续贴着底。</summary>
        public void ContentGrew()
        {
            if (Stick) Offset = MaxOffset;
            Apply();
        }

        public void ScrollBy(float dy)
        {
            float o = Mathf.Clamp(Offset + dy, 0f, MaxOffset);
            if (Mathf.Abs(o - Offset) < 0.01f) return;
            Offset = o;
            Stick = Offset >= MaxOffset - 0.5f;
            Apply();
        }

        public void ToBottom()
        {
            Offset = MaxOffset;
            Stick = true;
            Apply();
        }

        public void ToTop()
        {
            Offset = 0f;
            Stick = false;
            Apply();
        }

        /// <summary>把 Offset 写进内容位置 + 刷滚动条。</summary>
        public void Apply()
        {
            if (MaxOffset <= 0f) Offset = 0f;
            if (Content != null)
            {
                // 符号容易写反：Ui.Place 的 y 是从父容器顶边「往下」量的，
                // 而 Offset 是「往下滚了多远」——往下滚意味着内容整体上移，所以这里是 -Offset。
                // 写成 +Offset 的话，内容会被往下推，越滚越看不见东西。
                try { Ui.Place(Content.gameObject, 0f, -Offset, ViewW, ContentH); } catch { }
            }
            if (Bar != null && BarRect != null)
            {
                try
                {
                    if (ContentH <= ViewH + 0.5f)
                    {
                        Bar.gameObject.SetActive(false);
                    }
                    else
                    {
                        Bar.gameObject.SetActive(true);
                        float barH = Mathf.Max(28f, H * (ViewH / ContentH));
                        float maxBar = H - barH;
                        float t = MaxOffset <= 0f ? 0f : Offset / MaxOffset;
                        Ui.Place(Bar.gameObject, X + W - BarW, Y + maxBar * t, BarW, barH);
                    }
                }
                catch { }
            }
        }

        public bool ContainsMouse()
        {
            try
            {
                if (ViewRect == null) return false;
                return RectTransformUtility.RectangleContainsScreenPoint(ViewRect, Input.mousePosition, null);
            }
            catch { return false; }
        }

        /// <summary>每帧调一次：接管滚轮。</summary>
        public void PollWheel()
        {
            try
            {
                if (!ContainsMouse()) return;
                float d = Input.mouseScrollDelta.y;
                if (Mathf.Abs(d) < 0.01f) return;
                ScrollBy(-d * 62f);
            }
            catch { }
        }
    }
}
