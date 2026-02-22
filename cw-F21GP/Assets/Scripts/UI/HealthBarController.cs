using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace F21GP.UI
{
    public class HealthBarController : MonoBehaviour
    {
        [SerializeField] private Image _healthSlider;
        [SerializeField] private TextMeshProUGUI _healthText;

        public void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            _healthSlider.fillAmount = currentHealth / maxHealth;
            _healthText.text = currentHealth.ToString("F0");
        }
    }
}
