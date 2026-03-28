using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPI lookup panel on the computer screen. Player enters an NPI number and gets
/// prescriber info back (or "not found" for invalid/fake NPIs).
///
/// Attach to a panel inside one of the ComputerScreenController views.
/// Requires a TMP_InputField for the search query and TMP text elements for results.
/// </summary>
public class NPISearchPanel : MonoBehaviour
{
    [Header("Database")]
    [Tooltip("The PrescriberDatabase asset containing all valid prescriber entries.")]
    [SerializeField] private PrescriberDatabase database;

    [Header("UI — Input")]
    [Tooltip("The input field where the player types the NPI number.")]
    [SerializeField] private TMP_InputField npiInputField;

    [Tooltip("Button that triggers the search. Optional — pressing Enter in the input field also works.")]
    [SerializeField] private Button searchButton;

    [Header("UI — Results")]
    [Tooltip("Parent panel for result text elements. Hidden when no search has been performed.")]
    [SerializeField] private GameObject resultsPanel;

    [Tooltip("Displays the prescriber's name (or 'Not Found').")]
    [SerializeField] private TextMeshProUGUI resultNameText;

    [Tooltip("Displays the prescriber's specialty.")]
    [SerializeField] private TextMeshProUGUI resultSpecialtyText;

    [Tooltip("Displays the prescriber's address.")]
    [SerializeField] private TextMeshProUGUI resultAddressText;

    [Tooltip("Displays the NPI number that was searched.")]
    [SerializeField] private TextMeshProUGUI resultNPIText;

    [Header("UI — Status")]
    [Tooltip("Text element that shows 'No results found' or validation messages.")]
    [SerializeField] private TextMeshProUGUI statusText;

    void Start()
    {
        if (searchButton != null)
            searchButton.onClick.AddListener(PerformSearch);

        if (npiInputField != null)
            npiInputField.onSubmit.AddListener(_ => PerformSearch());

        // Start with results hidden
        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (statusText != null)
            statusText.text = "Enter an NPI number to search.";
    }

    /// <summary>
    /// Performs the NPI lookup using the current input field value.
    /// Called by the search button or Enter key.
    /// </summary>
    public void PerformSearch()
    {
        if (database == null)
        {
            ShowStatus("Error: No prescriber database assigned.");
            return;
        }

        if (npiInputField == null)
        {
            ShowStatus("Error: No input field assigned.");
            return;
        }

        string query = npiInputField.text.Trim();

        if (string.IsNullOrEmpty(query))
        {
            ShowStatus("Please enter an NPI number.");
            HideResults();
            return;
        }

        PrescriberEntry entry = database.LookupByNPI(query);

        if (entry != null)
        {
            ShowResults(entry);
            ShowStatus("");
        }
        else
        {
            HideResults();
            ShowStatus($"NPI '{query}' not found in database.");
        }
    }

    /// <summary>Clears the input field and results. Called when the computer screen deactivates.</summary>
    public void ResetPanel()
    {
        if (npiInputField != null)
            npiInputField.text = string.Empty;

        HideResults();

        if (statusText != null)
            statusText.text = "Enter an NPI number to search.";
    }

    private void ShowResults(PrescriberEntry entry)
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (resultNameText != null)
            resultNameText.text = entry.prescriberName;

        if (resultSpecialtyText != null)
            resultSpecialtyText.text = entry.specialty;

        if (resultAddressText != null)
            resultAddressText.text = entry.address;

        if (resultNPIText != null)
            resultNPIText.text = entry.npi;
    }

    private void HideResults()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
