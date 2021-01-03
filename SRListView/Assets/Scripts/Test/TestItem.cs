// ******************************************************************
//       /\ /|       @file       TestItem.cs
//       \ V/        @brief      测试item
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-01-03 10:30:26
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using UnityEngine;
using UnityEngine.UI;

namespace SR.ListView
{
    public class TestItem
    {
        private string _data;
        private Transform _transform;

        /// <summary>
        /// item启用时回调
        /// </summary>
        /// <param name="data"></param>
        public void OnEnabled(string data, Transform transform)
        {
            _data = data;
            _transform = transform;
            //todo 注册点击监听   
        }

        /// <summary>
        /// item禁用时回调
        /// </summary>
        public void OnDisabled()
        {
            _data = string.Empty;
            _transform = null;
            //todo 撤销监听
        }

        /// <summary>
        /// 刷新UI界面
        /// </summary>
        public void Refresh()
        {
            _transform.GetComponentInChildren<Text>().text = _data;
        }
    }
}