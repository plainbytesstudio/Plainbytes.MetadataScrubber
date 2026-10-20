// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Resources;

namespace PlainBytes.Core.Resources;

public static class Strings
{
    private static readonly ResourceManager Manager =
        new("PlainBytes.Core.Resources.Strings", typeof(Strings).Assembly);

    public static string App_Title => Get(nameof(App_Title));
    public static string Nav_Home => Get(nameof(Nav_Home));
    public static string Nav_Settings => Get(nameof(Nav_Settings));
    public static string Nav_Help => Get(nameof(Nav_Help));
    public static string Nav_About => Get(nameof(Nav_About));
    public static string Nav_Matrix => Get(nameof(Nav_Matrix));
    public static string Nav_Support => Get(nameof(Nav_Support));
    public static string Rate_Title => Get(nameof(Rate_Title));
    public static string Rate_Description => Get(nameof(Rate_Description));
    public static string Rate_Action => Get(nameof(Rate_Action));
    public static string Rate_Never => Get(nameof(Rate_Never));
    public static string Support_Title => Get(nameof(Support_Title));
    public static string Support_Subtitle => Get(nameof(Support_Subtitle));
    public static string Support_BadgeUnlocked => Get(nameof(Support_BadgeUnlocked));
    public static string Support_Thanks => Get(nameof(Support_Thanks));
    public static string Support_TierCoffee_Title => Get(nameof(Support_TierCoffee_Title));
    public static string Support_TierCoffee_Description => Get(nameof(Support_TierCoffee_Description));
    public static string Support_TierEnergy_Title => Get(nameof(Support_TierEnergy_Title));
    public static string Support_TierEnergy_Description => Get(nameof(Support_TierEnergy_Description));
    public static string Support_TierDev_Title => Get(nameof(Support_TierDev_Title));
    public static string Support_TierDev_Description => Get(nameof(Support_TierDev_Description));
    public static string Support_BenefitsHeading => Get(nameof(Support_BenefitsHeading));
    public static string Support_BenefitBadge => Get(nameof(Support_BenefitBadge));
    public static string Support_BenefitThanks => Get(nameof(Support_BenefitThanks));
    public static string Support_PaymentNote => Get(nameof(Support_PaymentNote));
    public static string Support_Once => Get(nameof(Support_Once));
    public static string Support_Button => Get(nameof(Support_Button));
    public static string Support_Popular => Get(nameof(Support_Popular));
    public static string Matrix_Title => Get(nameof(Matrix_Title));
    public static string Matrix_Subtitle => Get(nameof(Matrix_Subtitle));
    public static string Matrix_MaintenanceNote => Get(nameof(Matrix_MaintenanceNote));
    public static string Matrix_Installed => Get(nameof(Matrix_Installed));
    public static string Matrix_LearnMore => Get(nameof(Matrix_LearnMore));
    public static string Catalog_MetadataScrubber_Description => Get(nameof(Catalog_MetadataScrubber_Description));
    public static string Catalog_ImageConverter_Description => Get(nameof(Catalog_ImageConverter_Description));
    public static string Catalog_DocumentExtractor_Description => Get(nameof(Catalog_DocumentExtractor_Description));
    public static string Catalog_Redactor_Description => Get(nameof(Catalog_Redactor_Description));
    public static string Catalog_FileRenamer_Description => Get(nameof(Catalog_FileRenamer_Description));
    public static string Catalog_ImageCompressor_Description => Get(nameof(Catalog_ImageCompressor_Description));
    public static string Catalog_PdfMerger_Description => Get(nameof(Catalog_PdfMerger_Description));
    public static string Catalog_ClipboardGuard_Description => Get(nameof(Catalog_ClipboardGuard_Description));
    public static string License_TrialTitleSuffix => Get(nameof(License_TrialTitleSuffix));
    public static string License_TrialTitleSuffixDays => Get(nameof(License_TrialTitleSuffixDays));
    public static string License_TrialTitleSuffixDay => Get(nameof(License_TrialTitleSuffixDay));
    public static string License_TrialTitleSuffixLessThanDay => Get(nameof(License_TrialTitleSuffixLessThanDay));
    public static string License_ExpiredTitle => Get(nameof(License_ExpiredTitle));
    public static string License_ExpiredMessage => Get(nameof(License_ExpiredMessage));
    public static string License_CloseApp => Get(nameof(License_CloseApp));
    public static string License_BuyNow => Get(nameof(License_BuyNow));
    public static string License_BuyNowWithPrice => Get(nameof(License_BuyNowWithPrice));
    public static string License_PurchasePromptTitle => Get(nameof(License_PurchasePromptTitle));
    public static string License_PurchasePromptMessage => Get(nameof(License_PurchasePromptMessage));
    public static string License_ContinueTrial => Get(nameof(License_ContinueTrial));
    public static string License_ContinueTrialDays => Get(nameof(License_ContinueTrialDays));
    public static string License_ContinueTrialDay => Get(nameof(License_ContinueTrialDay));
    public static string License_PurchasedTitle => Get(nameof(License_PurchasedTitle));
    public static string License_PurchasedMessage => Get(nameof(License_PurchasedMessage));
    public static string License_GotIt => Get(nameof(License_GotIt));

    public static string Format(string template, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, template ?? "", args);

    private static string Get(string name) =>
        Manager.GetString(name, CultureInfo.CurrentUICulture) ?? "";
}
