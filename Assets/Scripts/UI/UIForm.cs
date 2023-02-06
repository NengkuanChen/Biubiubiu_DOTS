using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace UI
{
    public abstract class UIForm: MonoBehaviour
    {

        [SerializeField] 
        private List<UIElement> elements;
        public List<UIElement> Elements => elements;

        

        public static bool IsOpen<T>() where T: UIForm
        {
            return UIManager.Singleton.GetForm<T>()?.gameObject.activeSelf ?? false;
        }
        
        public static T Singleton<T>() where T: UIForm
        {
            return UIManager.Singleton.GetForm<T>();
        }
        
        public static void Open<T>() where T: UIForm
        {
            UIManager.Singleton.ShowForm<T>();
        }
        
        public static void Close<T>() where T: UIForm
        {
            UIManager.Singleton.CloseForm<T>();
        }

        public virtual void OnInitialize()
        {
            foreach (var element in elements)
            {
                element.OnInitialize();
            }
        }
        
        public void CloseSelf()
        {
            UIManager.Singleton.CloseForm(this);
        }

        public virtual void OnShow()
        {
            foreach (var uiElement in elements)
            {
                uiElement.OnShow();
            }
        }
        
        public virtual void OnClose()
        {
            foreach (var uiElement in elements)
            {
                uiElement.OnClose();
            }
        }
        
        
        
    }
}