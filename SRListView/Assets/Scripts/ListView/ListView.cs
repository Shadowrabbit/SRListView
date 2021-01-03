// ******************************************************************
//       /\ /|       @file       ListView.cs
//       \ V/        @brief      循环列表UI组件
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-01-02 12:51:59
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SR.ListView
{
    //布局方向
    public enum Direction
    {
        Horizontal, //水平
        Vertical, //垂直
    }

    //编辑器保持生命周期
    [ExecuteInEditMode]
    public class ListView : MonoBehaviour
    {
        #region FIELDS

        public float spacingX; //x间距
        public float spacingY; //y间距
        public float offsetX; //偏移量x
        public float offsetY; //偏移量y
        public GameObject protoObj; //item原型体
        public Direction direction = Direction.Vertical; //布局方向
        public int rowOrCol = 1; //垂直布局表示列的数量 水平布局表示行的数量
        private Action<GameObject, int> _onEnabled; //启用时回调 
        private Action<int> _onDisabled; //禁用时回调 
        private GameObject _viewport; //可见范围遮罩节点
        private GameObject _content; //内容节点
        private RectTransform _compRectTransformContent; //内容节点tran组件
        private RectTransform _compRectTransformViewport; //遮罩节点tran组件
        private ScrollRect _scrollRect; //滑动组件
        private float _protoHeight; //原型体高度
        private float _protoWidth; //原型体宽度
        private int _cacheItemNum = -1; //当前数据中item的总数量
        private List<Vector3> _objPosList = new List<Vector3>(); //obj物体位置列表
        private Stack<GameObject> _objPool = new Stack<GameObject>(); //item物体对象池

        private Dictionary<int, GameObject>
            _mapCurrentObjs = new Dictionary<int, GameObject>(); //当前视野内的obj物体<索引,物体>

        #endregion

        /// <summary>
        /// 注册item启用回调
        /// </summary>
        /// <param name="onItemEnabled"></param>
        public void AddListenerOnItemEnabled(Action<GameObject, int> onItemEnabled)
        {
            if (onItemEnabled == null)
            {
                return;
            }

            _onEnabled = onItemEnabled;
        }

        /// <summary>
        /// 注册item禁用回调
        /// </summary>
        /// <param name="onItemDisabled"></param>
        public void AddListenerOnItemDisabled(Action<int> onItemDisabled)
        {
            if (onItemDisabled == null)
            {
                return;
            }

            _onDisabled = onItemDisabled;
        }

        /// <summary>
        /// 注册滑动进度改变回调
        /// </summary>
        /// <param name="onValueChanged"></param>
        public void AddListenerOnValueChanged(UnityAction<Vector2> onValueChanged)
        {
            if (onValueChanged == null)
            {
                return;
            }

            _scrollRect.onValueChanged.AddListener(onValueChanged);
        }

        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="itemNum">item绘制数量</param>
        public void Refresh(int itemNum)
        {
            //尝试回收多余的item
            TryRecycleItems(itemNum);
            // 计算content尺寸
            CalcContentSize(itemNum);
            //计算储存每个item坐标信息
            CalcEachItemPosition(itemNum);
            //尝试放置item
            TrySetItems();
            //记录当前item总数量
            _cacheItemNum = itemNum;
        }

        /// <summary>
        /// 清空所有item
        /// </summary>
        public void ClearItems()
        {
            for (var i = _compRectTransformContent.childCount - 1; i >= 0; i--)
            {
                var compTransformChild = _compRectTransformContent.GetChild(i);
                DestroyImmediate(compTransformChild.gameObject);
            }

            _objPool.Clear();
            _mapCurrentObjs.Clear();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            //原型体校验
            CheckProtoTransform();
            //滑动组件校验
            CheckScrollRect();
            //内容节点校验
            CheckContentTransform();
            //监听滑动进度改变
            _scrollRect.onValueChanged.AddListener(RebuildLayout);
        }

        /// <summary>
        /// 尝试回收多余item
        /// </summary>
        /// <param name="itemNum"></param>
        private void TryRecycleItems(int itemNum)
        {
            //当前需要绘制的item总数大于等于缓存中的item总数
            if (itemNum >= _cacheItemNum)
            {
                return;
            }

            //回收多余item
            for (var i = itemNum; i < _cacheItemNum; i++)
            {
                TryRecycleItem(i);
            }
        }

        /// <summary>
        /// 尝试回收item
        /// </summary>
        private void TryRecycleItem(int i)
        {
            _mapCurrentObjs.TryGetValue(i, out var obj);
            //不存在对应索引的物体
            if (obj == null)
            {
                return;
            }

            //回收物体
            _onDisabled?.Invoke(i);
            PushObj(obj);
            _mapCurrentObjs.Remove(i);
        }

        /// <summary>
        /// 尝试放置item
        /// </summary>  
        private void TrySetItems()
        {
            for (var i = 0; i < _objPosList.Count; i++)
            {
                //超出可见范围
                if (IsOutOfVisitableRange(_objPosList[i]))
                {
                    //回收物体
                    TryRecycleItem(i);
                    continue;
                }

                //没有超出可见范围
                _mapCurrentObjs.TryGetValue(i, out var obj);
                //物体不存在
                if (obj == null)
                {
                    //物体不存在 尝试从对象池中取出
                    obj = PopObj();
                    _mapCurrentObjs.Add(i, obj);
                }

                obj.transform.localPosition = _objPosList[i];
                obj.gameObject.name = protoObj.name + (i + 1);
                //启用回调
                _onEnabled?.Invoke(obj, i);
            }
        }

        /// <summary>
        /// 计算储存每个item坐标信息
        /// </summary>
        /// <param name="itemNum"></param>
        private void CalcEachItemPosition(int itemNum)
        {
            if (itemNum < 0)
            {
                return;
            }

            //清空数据
            _objPosList.Clear();
            for (var i = 0; i < itemNum; i++)
            {
                float x, y;
                switch (direction)
                {
                    case Direction.Vertical:
                        x = (i % rowOrCol) * (_protoWidth + spacingX) + offsetX;
                        // ReSharper disable once PossibleLossOfFraction
                        y = (_protoHeight + spacingY) * (i / rowOrCol) + offsetY;
                        break;
                    case Direction.Horizontal:
                        // ReSharper disable once PossibleLossOfFraction
                        x = (_protoWidth + spacingX) * (i / rowOrCol) + offsetX;
                        y = (i % rowOrCol) * (_protoHeight + spacingY) + offsetY;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _objPosList.Add(new Vector3(x, -y, 0));
            }
        }

        /// <summary>
        /// obj出栈
        /// </summary>
        /// <returns></returns>
        private GameObject PopObj()
        {
            //尝试从对象池取obj 没有的话创建新实例
            var obj = _objPool.Count > 0 ? _objPool.Pop() : Instantiate(protoObj);
            obj.transform.SetParent(_content.transform);
            obj.transform.localScale = Vector3.one;
            obj.SetActive(true);
            return obj;
        }

        /// <summary>
        /// obj压入对象池栈
        /// </summary>
        /// <param name="obj"></param>
        private void PushObj(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            _objPool.Push(obj);
            obj.SetActive(false);
        }

        /// <summary>
        /// 原型体transform校验
        /// </summary>
        private void CheckProtoTransform()
        {
            //原型体校验
            if (protoObj == null)
            {
                Debug.LogError("找不到原型体");
                return;
            }

            var compProtoRectTransform = protoObj.GetComponent<RectTransform>();
            compProtoRectTransform.pivot = new Vector2(0, 1);
            compProtoRectTransform.anchorMin = new Vector2(0, 1);
            compProtoRectTransform.anchorMax = new Vector2(0, 1);
            compProtoRectTransform.anchoredPosition = Vector2.zero;
            var rect = compProtoRectTransform.rect;
            _protoHeight = rect.height;
            _protoWidth = rect.width;
        }

        /// <summary>
        /// 内容节点transform校验
        /// </summary>
        private void CheckContentTransform()
        {
            _viewport = _scrollRect.viewport.gameObject;
            if (_viewport == null)
            {
                Debug.LogError("找不到viewport!");
                return;
            }

            _compRectTransformViewport = _viewport.GetComponent<RectTransform>();
            if (_compRectTransformViewport == null)
            {
                _compRectTransformViewport = _viewport.AddComponent<RectTransform>();
            }

            _content = _scrollRect.content.gameObject;
            if (_content == null)
            {
                Debug.LogError("找不到content!");
                return;
            }

            _compRectTransformContent = _content.GetComponent<RectTransform>();
            if (_compRectTransformContent == null)
            {
                _compRectTransformContent = _content.AddComponent<RectTransform>();
            }

            _compRectTransformContent.pivot = new Vector2(0, 1);
            _compRectTransformContent.anchorMin = new Vector2(0, 1);
            _compRectTransformContent.anchorMax = new Vector2(0, 1);
        }

        /// <summary>
        /// 滑动组件校验
        /// </summary>
        private void CheckScrollRect()
        {
            _scrollRect = GetComponent<ScrollRect>();
            if (_scrollRect == null)
            {
                gameObject.AddComponent<ScrollRect>();
            }
        }

        /// <summary>
        /// 重绘布局 更新item
        /// </summary>
        /// <param name="value"></param>
        private void RebuildLayout(Vector2 value)
        {
            TrySetItems();
        }

        /// <summary>
        /// 计算content尺寸
        /// </summary>
        private void CalcContentSize(int itemNum)
        {
            if (itemNum < 0)
            {
                Debug.LogError("itemNum不能小于0");
                return;
            }

            var eachRowOrColNum = Mathf.Ceil((float) itemNum / rowOrCol); //每列/行item的最大数量
            switch (direction)
            {
                //垂直布局
                case Direction.Vertical:
                {
                    var contentHeight =
                        (spacingY + _protoHeight) * eachRowOrColNum +
                        offsetY; //内容布局高 = (原型体高度+间隔高度) * 每列的最大item数量 + 垂直偏移
                    var contentWidth =
                        _protoWidth * rowOrCol + (rowOrCol - 1) * spacingX +
                        offsetX; //内容布局宽 = 原型体宽度 * 列数 + (列数-1) * 间距X + 水平偏移
                    _compRectTransformContent.sizeDelta = new Vector2(contentWidth, contentHeight);
                    break;
                }
                //水平布局
                case Direction.Horizontal:
                {
                    var contentWidth = (spacingX + _protoWidth) * eachRowOrColNum;
                    var contentHeight = _protoHeight * rowOrCol + (rowOrCol - 1) * spacingY;
                    _compRectTransformContent.sizeDelta = new Vector2(contentWidth, contentHeight);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 是否超出可见范围
        /// </summary>
        /// <param name="position">item坐标</param>
        /// <returns></returns>
        private bool IsOutOfVisitableRange(Vector3 position)
        {
            var posContent = _compRectTransformContent.anchoredPosition; //content的坐标
            switch (direction)
            {
                //自身偏移+content偏移>原型体高度 顶部越界
                case Direction.Vertical when position.y + posContent.y > _protoHeight:
                    return true;
                //自身偏移+content偏移<-遮罩高度 底部越界
                case Direction.Vertical when position.y + posContent.y < -_compRectTransformViewport.rect.height:
                    return true;
                case Direction.Vertical:
                    return false;
                //自身偏移+content偏移<-原型体宽度 左部越界
                case Direction.Horizontal when position.x + posContent.x < -_protoWidth:
                    return true;
                //自身偏移+content偏移>遮罩宽度 右部越界
                case Direction.Horizontal when position.x + posContent.x > _compRectTransformViewport.rect.width:
                    return true;
                case Direction.Horizontal:
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>   
        /// 释放内存
        /// </summary>
        private void Dispose()
        {
            _scrollRect.onValueChanged.RemoveAllListeners();
            _onEnabled = null;
            _onDisabled = null;
            _objPosList = null;
            _objPool = null;
            _mapCurrentObjs = null;
            protoObj = null;
            _viewport = null;
            _content = null;
            _compRectTransformContent = null;
            _compRectTransformViewport = null;
            _scrollRect = null;
        }

        #region UNITY LIFE

        private void Awake()
        {
            Init();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// inspector值发生改变回调
        /// </summary>
        private void OnValidate()
        {
            CalcEachItemPosition(_cacheItemNum);
            TrySetItems();
        }

        #endregion
    }
}