using System;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using Sitecore.SecurityModel;

namespace Sitecore.MarkdownForAI.Install
{
    /// <summary>Field names of the <c>_LlmsPage</c> base template.</summary>
    public static class LlmsFields
    {
        public const string Include = "LlmsInclude";
        public const string Optional = "LlmsOptional";
        public const string Title = "LlmsTitle";
        public const string Summary = "LlmsSummary";
        public const string Section = "LlmsSection";
    }

    /// <summary>Fixed IDs of the items the module creates, so every install and every environment matches.</summary>
    public static class ItemIds
    {
        public static readonly ID TemplatesFolder = new ID("{1C082D5A-FA42-4765-9B3D-72DD6D01D1D5}");
        public static readonly ID LlmsPageTemplate = new ID("{5DF73228-24F4-4350-852D-169D884FFA28}");
        public static readonly ID LlmsSection = new ID("{9497D868-2DED-444A-B0BF-05BA2A2A8666}");
        public static readonly ID LlmsIncludeField = new ID("{77D96D4D-04F6-48C7-A265-45EFAB7DC36E}");
        public static readonly ID LlmsOptionalField = new ID("{4B350638-943D-45C5-8366-17B2ABCDF56C}");
        public static readonly ID LlmsTitleField = new ID("{BF9A3486-4894-4B64-8FCC-A10E5036634A}");
        public static readonly ID LlmsSummaryField = new ID("{533F62F5-5247-4592-A066-F8BDFE44E2F1}");
        public static readonly ID LlmsSectionField = new ID("{358B1505-86BD-4FF3-9762-D94C06792652}");
        public static readonly ID ParametersTemplate = new ID("{79C0AFAF-6C18-4668-8F26-44707F0980A8}");
        public static readonly ID ParametersSection = new ID("{6D2DFA93-2B7E-4B94-8AEA-A59F0781A183}");
        public static readonly ID ModeField = new ID("{FF081F50-E6EA-4CDF-930D-D92DBBDAEBF3}");
        public static readonly ID SystemFolder = new ID("{E9F3FD1E-DEA0-41C6-817A-B84CE1917EC4}");
        public static readonly ID ModesFolder = new ID("{B906CF70-5553-4B13-95FA-8D6202AB5440}");
        public static readonly ID IncludeOption = new ID("{F7940BC3-6E33-4201-AACD-D3B2991CCF12}");
        public static readonly ID ExcludeOption = new ID("{CCD9C2DC-6366-43A2-ACD5-AC3EF1D1CD39}");
        public static readonly ID SystemModulesFolder = new ID("{611CA55C-D289-4B1F-BAAF-AED210D759DD}");
        /// <summary>The ID Sitecore PowerShell Extensions and the Sitecore package use for <c>/sitecore/templates/Modules</c>.</summary>
        public static readonly ID TemplateModulesFolder = new ID("{E6904C9A-3ACE-4B53-B465-4C05C6B1F1CC}");

        /// <summary>Sitecore's "Standard Rendering Parameters" template.</summary>
        public static readonly ID StandardRenderingParameters = new ID("{8CA06D6A-B353-44E8-BC31-B528C7306971}");
    }

    /// <summary>
    /// initialize pipeline (content management only). Creates the module's templates and
    /// option items in master if they are missing. Idempotent, never overwrites edits.
    /// Turn off with the <c>MarkdownForAI.InstallItems</c> setting once items are serialized.
    /// </summary>
    public class InstallItems
    {
        public const string ModesPath = "/sitecore/system/Modules/Markdown For AI/Modes";

        public void Process(PipelineArgs args)
        {
            if (!MarkdownSettings.InstallItems)
            {
                return;
            }

            try
            {
                Database master = Factory.GetDatabase("master", false);
                if (master == null)
                {
                    return;
                }

                using (new SecurityDisabler())
                {
                    Install(master);
                }
            }
            catch (Exception ex)
            {
                Log.Error("MarkdownForAI: failed to install items.", ex, this);
            }
        }

        public static void Install(Database database)
        {
            Item templatesRoot = database.GetItem("/sitecore/templates");
            Item systemRoot = database.GetItem("/sitecore/system");
            if (templatesRoot == null || systemRoot == null)
            {
                return;
            }

            Item templateModules = database.GetItem("/sitecore/templates/Modules") ??
                                   Create(templatesRoot, "Modules", TemplateIDs.TemplateFolder, ItemIds.TemplateModulesFolder);
            Item folder = Ensure(database, templateModules, "Markdown For AI", TemplateIDs.TemplateFolder, ItemIds.TemplatesFolder);

            // _LlmsPage: add as a base template to page templates that should appear in llms.txt.
            // Include and Optional are shared; title, summary and section are unversioned (translatable).
            Item llmsPage = EnsureTemplate(database, folder, "_LlmsPage", ItemIds.LlmsPageTemplate, TemplateIDs.StandardTemplate);
            Item llmsSection = Ensure(database, llmsPage, "LLMs", TemplateIDs.TemplateSection, ItemIds.LlmsSection);
            EnsureField(database, llmsSection, LlmsFields.Include, ItemIds.LlmsIncludeField, "Checkbox", "Include in llms.txt", 100, null);
            EnsureField(database, llmsSection, LlmsFields.Optional, ItemIds.LlmsOptionalField, "Checkbox", "List under Optional in llms.txt", 200, null);
            EnsureField(database, llmsSection, LlmsFields.Title, ItemIds.LlmsTitleField, "Single-Line Text", "Title for AI agents (optional)", 300, null, true);
            EnsureField(database, llmsSection, LlmsFields.Summary, ItemIds.LlmsSummaryField, "Multi-Line Text", "One-sentence summary for AI agents", 400, null, true);
            EnsureField(database, llmsSection, LlmsFields.Section, ItemIds.LlmsSectionField, "Single-Line Text", "Section heading in llms.txt", 500, null, true);

            // Option items for the Markdown mode droplist.
            Item systemModules = database.GetItem("/sitecore/system/Modules") ??
                                 Create(systemRoot, "Modules", TemplateIDs.Folder, ItemIds.SystemModulesFolder);
            Item systemFolder = Ensure(database, systemModules, "Markdown For AI", TemplateIDs.Folder, ItemIds.SystemFolder);
            Item modes = Ensure(database, systemFolder, "Modes", TemplateIDs.Folder, ItemIds.ModesFolder);
            Ensure(database, modes, "include", TemplateIDs.Folder, ItemIds.IncludeOption);
            Ensure(database, modes, "exclude", TemplateIDs.Folder, ItemIds.ExcludeOption);

            // _MarkdownRenderingParameters: use as (or inherit into) a rendering's Parameters Template.
            Item parameters = EnsureTemplate(database, folder, "_MarkdownRenderingParameters", ItemIds.ParametersTemplate, ItemIds.StandardRenderingParameters);
            Item parametersSection = Ensure(database, parameters, "Markdown", TemplateIDs.TemplateSection, ItemIds.ParametersSection);
            EnsureField(database, parametersSection, MarkdownSettings.ParameterName, ItemIds.ModeField, "Droplist", "Markdown mode (empty = component default)", 100, ModesPath);
        }

        private static Item EnsureTemplate(Database database, Item parent, string name, ID id, ID baseTemplate)
        {
            Item existing = database.GetItem(id);
            if (existing != null)
            {
                return existing;
            }

            Item template = Create(parent, name, TemplateIDs.Template, id);
            using (new EditContext(template))
            {
                template[FieldIDs.BaseTemplate] = baseTemplate.ToString();
            }

            return template;
        }

        private static void EnsureField(Database database, Item section, string name, ID id, string type, string title, int sortOrder, string source, bool unversioned = false)
        {
            if (database.GetItem(id) != null)
            {
                return;
            }

            Item field = Create(section, name, TemplateIDs.TemplateField, id);
            using (new EditContext(field))
            {
                field[TemplateFieldIDs.Type] = type;
                field[TemplateFieldIDs.Title] = title;
                if (unversioned)
                {
                    field[TemplateFieldIDs.Unversioned] = "1";
                }
                else
                {
                    field[TemplateFieldIDs.Shared] = "1";
                }

                field[FieldIDs.Sortorder] = sortOrder.ToString();
                if (!string.IsNullOrEmpty(source))
                {
                    field[TemplateFieldIDs.Source] = source;
                }
            }
        }

        private static Item Ensure(Database database, Item parent, string name, ID templateId, ID id)
        {
            return database.GetItem(id) ?? Create(parent, name, templateId, id);
        }

        private static Item Create(Item parent, string name, ID templateId, ID id)
        {
            Log.Info("MarkdownForAI: creating " + parent.Paths.FullPath + "/" + name, typeof(InstallItems));
            return ItemManager.AddFromTemplate(name, templateId, parent, id);
        }
    }
}
