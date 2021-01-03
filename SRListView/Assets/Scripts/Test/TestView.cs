// ******************************************************************
//       /\ /|       @file       TestView.cs
//       \ V/        @brief      
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-01-02 17:40:15
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using System.Collections.Generic;
using UnityEngine;

namespace SR.ListView
{
    public class TestView : MonoBehaviour
    {
        private readonly Dictionary<int, TestItem> _mapItems = new Dictionary<int, TestItem>();

        private void Start()
        {
            var listView = GetComponent<ListView>();
            listView.AddListenerOnItemEnabled(OnItemEnabled);
            listView.AddListenerOnItemDisabled(OnItemDisabled);
            //listView.AddListenerOnValueChanged();
            listView.Refresh(50);
        }

        /// <summary>
        /// item启用回调
        /// </summary>
        /// <param name="item"></param>
        /// <param name="index"></param>
        private void OnItemEnabled(GameObject item, int index)
        {
            _mapItems.TryGetValue(index, out var testItem);
            if (testItem == null)
            {
                testItem = new TestItem();
                _mapItems.Add(index, testItem);
            }

            testItem.OnEnabled("test" + index, item.transform);
            testItem.Refresh();
        }

        /// <summary>
        /// item禁用回调
        /// </summary>
        /// <param name="index"></param>
        private void OnItemDisabled(int index)
        {
            //找到对应的item实例
            _mapItems.TryGetValue(index, out var testItem);
            testItem?.OnDisabled();
        }
    }
}