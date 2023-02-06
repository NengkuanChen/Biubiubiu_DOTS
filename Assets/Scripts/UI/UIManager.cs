using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class UIManager: MonoBehaviour
    {
        public static UIManager Singleton;
        
        private Dictionary<Type, UIForm> forms = new Dictionary<Type, UIForm>();
        
        public Dictionary<Type, UIForm> Forms => forms;

        private void Awake()
        {
            Singleton = this;
            foreach (var form in GetComponentsInChildren<UIForm>(true))
            {
                forms.Add(form.GetType(), form);
                form.OnInitialize();
            }
            Debug.Log($"{forms.Count} forms loaded");
        }
        
        public T GetForm<T>() where T: UIForm
        {
            if (forms.TryGetValue(typeof(T), out var form))
            {
                return (T) form;
            }
            return null;
        }
        
        public void ShowForm<T>() where T: UIForm
        {
            if (forms.TryGetValue(typeof(T), out var form))
            {
                form.gameObject.SetActive(true);
                form.OnShow();
            }
        }
        
        public void CloseForm<T>() where T: UIForm
        {
            if (forms.TryGetValue(typeof(T), out var form))
            {
                form.gameObject.SetActive(false);
                form.OnClose();
            }
        }
        
        public void CloseForm<T>(T form) where T: UIForm
        {
            form.gameObject.SetActive(false);
            form.OnClose();
        }
    }
}