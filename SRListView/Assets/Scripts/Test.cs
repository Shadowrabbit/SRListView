// ******************************************************************
//       /\ /|       @file       Test.cs
//       \ V/        @brief      
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-01-02 17:40:15
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using System;
using UnityEngine;

namespace SR.ListView
{
    public class Test : MonoBehaviour   
    {
        private void Start()
        {
            var listView = GetComponent<ListView>();
            listView.Init();
            listView.Refresh(50);
        }
    }
}