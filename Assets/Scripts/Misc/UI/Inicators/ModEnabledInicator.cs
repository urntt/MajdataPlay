using MajdataPlay.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MajdataPlay.UI.Inicators
{
    public class ModEnabledInicator : UIInicator
    {
        
        protected override void Awake()
        {
            base.Awake();
            var actionTo = ActionTo;
            if(actionTo == null)
            {
                actionTo = gameObject;
            }
            var settings = MajInstances.GameManager.Settings;
            // 结算页上的提示是“开启 Mod 将不保存成绩”：下落式成绩会单独保存，只开下落式时不提示
            var sceneName = gameObject.scene.name;
            var isResultScene = sceneName is "Result" or "TotalResult";
            actionTo.SetActive(isResultScene ? settings.IsAnyModActiveExceptPlayStyle() : settings.IsAnyModActive());
        }
    }
}