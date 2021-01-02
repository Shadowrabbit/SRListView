﻿// ******************************************************************
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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SR.ListView
{
    //布局方向
    public enum Direction
    {
        Horizontal, //水平
        Vertical, //垂直
    }

    public class ListView : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        public float spacingX; //x间距
        public float spacingY; //y间距
        public float offsetX; //偏移量x
        public float offsetY; //偏移量y
        public GameObject protoObj; //item原型体
        public Direction direction = Direction.Vertical; //布局方向
        public int rowOrCol = 1; //垂直布局表示列的数量 水平布局表示行的数量
        private Action<GameObject, int> _onEnabled; //启用时回调 
        private UnityAction<Vector2> _onValueChanged; //滑动进度改变回调
        private GameObject _viewport; //可见范围遮罩节点
        private GameObject _content; //内容节点
        private RectTransform _compRectTransformContent; //内容节点tran组件
        private RectTransform _compRectTransformViewport; //遮罩节点tran组件
        private ScrollRect _scrollRect; //滑动组件
        private float _protoHeight; //原型体高度
        private float _protoWidth; //原型体宽度
        private int _indexMin; //可见范围内的最小索引值
        private int _indexMax; //可见范围内的最大索引值
        private readonly List<Vector3> _objPosList = new List<Vector3>(); //obj物体位置列表
        private readonly Stack<GameObject> _objPool = new Stack<GameObject>(); //item物体对象池

        private readonly Dictionary<int, GameObject>
            _mapCurrentObjs = new Dictionary<int, GameObject>(); //当前视野内的obj物体<索引,物体>

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            //原型体校验
            CheckProtoTransform();
            //滑动组件校验
            CheckScrollRect();
            //内容节点校验
            CheckContentTransform();
            //监听滑动进度改变
            _scrollRect.onValueChanged.AddListener(RebuildLayout);
            if (_onValueChanged == null)
            {
                return;
            }

            _scrollRect.onValueChanged.AddListener(_onValueChanged);
        }


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
        /// 注册滑动进度改变回调
        /// </summary>
        /// <param name="onValueChanged"></param>
        public void AddListenerOnValueChanged(UnityAction<Vector2> onValueChanged)
        {
            if (onValueChanged == null)
            {
                return;
            }

            _onValueChanged = onValueChanged;
        }

        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="itemNum">item绘制数量</param>
        public void Refresh(int itemNum)
        {
            // 计算content尺寸
            CalcContentSize(itemNum);
            //计算储存每个item坐标信息
            CalcEachItemPosition(itemNum);
            //尝试放置item
            TrySetItems();
        }

        /// <summary>
        /// 尝试放置item
        /// </summary>  
        private void TrySetItems()
        {
            for (var i = 0; i < _objPosList.Count; i++)
            {
                _mapCurrentObjs.TryGetValue(i, out var obj);
                //超出可见范围
                if (IsOutOfVisitableRange(_objPosList[i]))
                {
                    //视野范围内不存在对应索引的物体
                    if (obj == null)
                    {
                        continue;
                    }

                    //视野范围内存在对应索引物体 回收
                    PushObj(obj);
                    _mapCurrentObjs.Remove(i);
                    continue;
                }

                //视野范围内 物体存在
                if (obj != null) continue;
                //物体不存在 尝试从对象池中取出
                obj = PopObj();
                obj.transform.localPosition = _objPosList[i];
                obj.gameObject.name = (i + 1).ToString();
                _mapCurrentObjs.Add(i, obj);
                //启用回调
                if (_onEnabled == null)
                {
                    Debug.LogWarning("没有设置item启用回调");
                    continue;
                }

                _onEnabled(obj, i);
            }
        }

        /// <summary>
        /// 计算储存每个item坐标信息
        /// </summary>
        /// <param name="itemNum"></param>
        private void CalcEachItemPosition(int itemNum)
        {
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
        /// 开始拖拽回调
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnBeginDrag(PointerEventData eventData)
        {
        }

        /// <summary>
        /// 结束拖拽回调
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnEndDrag(PointerEventData eventData)
        {
        }

        /// <summary>
        /// 拖拽中回调
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnDrag(PointerEventData eventData)
        {
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
        /// 释放
        /// </summary>
        private void Dispose()
        {
            _onEnabled = null;
            _scrollRect.onValueChanged.RemoveAllListeners();
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
        /// 销毁时回调
        /// </summary>
        private void OnDestroy()
        {
            Dispose();
        }
    }
}