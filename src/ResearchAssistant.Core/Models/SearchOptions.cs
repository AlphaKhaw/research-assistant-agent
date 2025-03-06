namespace ResearchAssistant.Core.Models;

public class SearchOptions
{
    // Basic search configuration
    public int MaxResults { get; set; } = 5; // Maximum number of results to return
    public bool IncludeUrls { get; set; } = true; // Whether to include URLs in results
    public bool IncludeSnippets { get; set; } = true; // Whether to include snippets
    public bool IncludeContent { get; set; } = false; // Whether to include full content

    // Site filtering
    public string SiteFilter { get; set; } = string.Empty; // Site domain to restrict search to
    public string SiteFilterMode { get; set; } = string.Empty; // "i" for include, "e" for exclude
    public string LinkSite { get; set; } = string.Empty; // Only return results that link to this site

    // Content filtering
    public string DateRestrict { get; set; } = string.Empty; // Format: 'd[number]', 'w[number]', 'm[number]', 'y[number]'
    public string ExactTerms { get; set; } = string.Empty; // Only include results with these exact terms
    public string ExcludeTerms { get; set; } = string.Empty; // Exclude results with these terms
    public string OrTerms { get; set; } = string.Empty; // Include results with any of these terms
    public string FileType { get; set; } = string.Empty; // Restrict results to specific file types (pdf, doc, etc.)

    // Language and region
    public string Language { get; set; } = string.Empty; // Language code (e.g. 'lang_en', 'lang_fr')
    public string Country { get; set; } = string.Empty; // Country code (e.g. 'countryUS', 'countryCA')

    // Rights and safety
    public string Rights { get; set; } = string.Empty; // License filter (e.g. 'cc_publicdomain', 'cc_attribute')
    public string SafeSearch { get; set; } = string.Empty; // "active" or "off"

    // Duplicate filtering
    public string DuplicateContentFilter { get; set; } = string.Empty; // "0" to turn off duplicate filtering, "1" to turn on
}
