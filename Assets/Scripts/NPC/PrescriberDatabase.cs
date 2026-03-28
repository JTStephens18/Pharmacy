using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single prescriber entry in the database.
/// Players can look up NPI numbers on the computer to verify prescriptions.
/// </summary>
[System.Serializable]
public class PrescriberEntry
{
    [Tooltip("The prescriber's full name.")]
    public string prescriberName;

    [Tooltip("National Provider Identifier number.")]
    public string npi;

    [Tooltip("Medical specialty (e.g. 'Cardiology', 'Family Medicine').")]
    public string specialty;

    [Tooltip("Office address.")]
    public string address;
}

/// <summary>
/// ScriptableObject containing valid prescriber records.
/// Used by the computer screen's NPI lookup feature to verify prescriptions.
/// Doppelgangers may have NPIs that don't exist in this database or that map to the wrong specialty.
/// Create via: Right-click in Project → Create → NPC → Prescriber Database
/// </summary>
[CreateAssetMenu(fileName = "PrescriberDatabase", menuName = "NPC/Prescriber Database")]
public class PrescriberDatabase : ScriptableObject
{
    [Tooltip("All valid prescriber entries in the game.")]
    public List<PrescriberEntry> prescribers = new List<PrescriberEntry>();

    // Built on first lookup for O(1) access.
    private Dictionary<string, PrescriberEntry> _npiLookup;

    /// <summary>
    /// Look up a prescriber by NPI number.
    /// Returns null if the NPI is not in the database (invalid/fake).
    /// </summary>
    public PrescriberEntry LookupByNPI(string npi)
    {
        if (string.IsNullOrEmpty(npi)) return null;

        if (_npiLookup == null)
            BuildLookup();

        _npiLookup.TryGetValue(npi.Trim(), out PrescriberEntry entry);
        return entry;
    }

    /// <summary>
    /// Returns true if the NPI exists in the database.
    /// </summary>
    public bool IsValidNPI(string npi)
    {
        return LookupByNPI(npi) != null;
    }

    private void BuildLookup()
    {
        _npiLookup = new Dictionary<string, PrescriberEntry>();
        foreach (var entry in prescribers)
        {
            if (!string.IsNullOrEmpty(entry.npi))
                _npiLookup[entry.npi.Trim()] = entry;
        }
    }

    private void OnEnable()
    {
        // Force rebuild on asset reload (editor hot-reload safety).
        _npiLookup = null;
    }
}
