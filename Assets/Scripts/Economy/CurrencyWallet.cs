using UnityEngine;

public class CurrencyWallet : MonoBehaviour
{
    private static CurrencyWallet instance;

    [Header("Currency")]
    [SerializeField] private int startingCurrency;
    [SerializeField] private int currentCurrency;

    public static CurrencyWallet Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<CurrencyWallet>();
            }

            return instance;
        }
    }

    public int CurrentCurrency => currentCurrency;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Multiple CurrencyWallet instances found. Using the first active instance.");
            return;
        }

        instance = this;
        currentCurrency = Mathf.Max(0, startingCurrency);
    }

    public void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentCurrency += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentCurrency < amount)
        {
            return false;
        }

        currentCurrency -= amount;
        return true;
    }
}
