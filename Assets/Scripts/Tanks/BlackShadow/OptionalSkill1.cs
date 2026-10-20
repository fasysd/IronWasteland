using System;
using UnityEngine;

public class OptionalSkill1 : MonoBehaviour
{
    public event Action<bool> OnIsEnhancedChange;
    private float remainingTime = 0;
    [SerializeField] private float timeEnhance = 10f;
    private bool isInvoke = false;
    public void Initialize()
    {
        remainingTime = timeEnhance;
        OnIsEnhancedChange?.Invoke(true);

    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(remainingTime > 0)
        {
            isInvoke = false;
            remainingTime -= Time.deltaTime;
       
        }
        if(remainingTime <= 0 && isInvoke == false)
        { 
            OnIsEnhancedChange?.Invoke(false);
            isInvoke = true;
        }
    }
}
