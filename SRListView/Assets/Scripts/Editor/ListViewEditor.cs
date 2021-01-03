// ******************************************************************
//       /\ /|       @file       ListViewEditor.cs
//       \ V/        @brief      listview扩展编辑器
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-01-03 14:56:32
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using System;
using UnityEditor;
using UnityEngine;

namespace SR.ListView
{
    [CustomEditor(typeof(ListView))]
    [Serializable]
    public class ListViewEditor : Editor
    {
        [SerializeField] public int tryDrawItemNum;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Label("*****预览*****");
            tryDrawItemNum = EditorGUILayout.IntField("尝试绘制数量", tryDrawItemNum);
            if (GUILayout.Button("重绘"))
            {
                ReDraw();
            }

            if (GUILayout.Button("清空"))
            {
                Clear();
            }
        }

        /// <summary>
        /// 清空
        /// </summary>
        private void Clear()
        {
            var listView = (ListView) target;
            listView.ClearItems();
            tryDrawItemNum = 0;
        }

        /// <summary>
        /// 重绘
        /// </summary>
        private void ReDraw()
        {
            var listView = (ListView) target;
            listView.ClearItems();
            listView.Refresh(tryDrawItemNum);
        }

        // [MenuItem("GameObject/UI/创建Vertical ListView")]
        // private static void CreateVerticalListView()
        // {
        //     Debug.Log("在GameObject目录里右键");
        // }
    }
}