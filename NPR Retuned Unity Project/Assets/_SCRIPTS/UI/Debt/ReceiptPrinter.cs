using System.Collections;
using TMPro;
using UnityEngine;

public class ReceiptPrinter : MonoBehaviour
{
    [SerializeField] private int recieptLoops;
    [SerializeField] private float recieptPause;
    [TextArea, SerializeField] private string headerText;
    [TextArea, SerializeField] private string footerText;
    [SerializeField] private TextMeshProUGUI receiptText;
    [SerializeField] private TextMeshProUGUI printerText;

    void Start()
    {
        receiptText.text = $"{headerText}\n" +
            $"FCC Fine \n-${Mathf.Round(PlayerStats.root.FCCFine * 100) / 100}\n" +
            $"Van Upkeep \n-${Mathf.Round(PlayerStats.root.VanUpkeep * 100) / 100}\n" +
            $"Total Earnings \n+${Mathf.Round(PlayerStats.root.LastRunMoney * 100) / 100}\n\n" +
            $"Final Total \n${Mathf.Round((PlayerStats.root.LastRunMoney - PlayerStats.root.FCCFine - PlayerStats.root.VanUpkeep) * 100) / 100}\n" +
            $"Current Balance \n${Mathf.Round(PlayerStats.root.CurrentMoney * 100) / 100}\n" +
            $"{footerText}";

        StartCoroutine(PrintReceipt());
        StartCoroutine(TypeOnPrinter());
    }

    private IEnumerator PrintReceipt()
    {
        yield return new WaitForSeconds(2f);
        GetComponent<Animator>().enabled = false;
        while (transform.localPosition.y < 2f)
        {
            for (int i = 0; i < recieptLoops; i++)
            {
                transform.localPosition += Vector3.up * Time.deltaTime;

                if (transform.localPosition.y >= 2f) break;

                yield return null;
            }

            yield return new WaitForSeconds(recieptPause);
        }

        while (PInputManager.root.actions[PlayerActionType.Find].fValue < 0.1f)
        {
            yield return null;
        }

        transform.localPosition = Vector3.up * 2f;

        StopCoroutine(TypeOnPrinter());

        GetComponent<Animator>().enabled = true;
        GetComponent<Animator>().SetTrigger("rip");

        yield return new WaitForSeconds(1f);

        GameManager.root.TriggerAfterLevelDialogue();
    }
    
    private IEnumerator TypeOnPrinter()
    {
        yield return new WaitForSeconds(2f);
        while(true)
        {
            for(int i = 0; i < 4; i++)
            {
                printerText.text = "Printing";

                if (i > 2) printerText.text += "...";
                else if (i > 1) printerText.text += "..";
                else if (i > 0) printerText.text += ".";
                
                yield return new WaitForSeconds(0.25f);
            }
        }
    }
}
