using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    [SerializeField] private Image _healthSlider;

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        _healthSlider.fillAmount = currentHealth / maxHealth;
    }
}
