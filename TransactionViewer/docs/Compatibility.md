# Compatibilité — TransactionViewer

## Environnement cible

* **IDE** : Visual Studio 2022
* **OS** : Windows 11
* **.NET** : .NET Framework **4.8.1**
* **Langage C#** : **7.3** (obligatoire)
* **Base de données** : SQL Server Express 2022 (instance locale `DOMINIC-DEV\SQLEXPRESS`)

## Règles C# 7.3

* ❌ Pas de `using var` (C# 8+)
* ❌ Pas de nullable reference types (`string?`) / `is not` pattern, `switch` expressions, ranges/slices.
* ✅ Toujours disposer explicitement :

  ```csharp
  var pd = new PrintDocument();
  var dlg = new PrintDialog();
  try { ... } finally { dlg.Dispose(); pd.Dispose(); }
  ```
* ✅ `TryParse` pour toutes conversions (dates, montants).

## Culture & formats

* **Culture** : `fr-CA`
* **Montants** : `N2` + espace + `$` → `144,00 $`
* **Dates** (affichage) : `dd/MM/yyyy`
* **Dates** (stockage SQL) : `datetime`/`datetime2` + conversions contrôlées côté DAL

## Namespaces officiels (doivent rester stables)

```
TransactionViewer
  Program, Form1
TransactionViewer.Models
  Transaction
TransactionViewer.DataAccess
  TransactionRepository
TransactionViewer.Helpers
  ClientRefHelper
TransactionViewer.Services
  JsonImportService, CsvExporter, ArchiveService
TransactionViewer.Printing
  PrintManager, PrintManagerFailed, PrintManagerException, IPrintManager
```

## Fichiers de configuration

* `App.config`

  * `connectionStrings:TransactionDb`
  * `appSettings:NsfOutputRoot` (optionnel)
  * `appSettings:ArchiveRoot` (optionnel)
* **Règle** : si une clé est absente → fallback `Documents\TransactionViewer\...`

## Données & Schéma minimal

* Table `Transactions` (extrait) :

  * `TransactionID (PK) nvarchar(50)`
  * `TransactionDateTime datetime` / `LastModified datetime`
  * `TransactionType`, `TransactionStatus`
  * `CreditAmount decimal(18,2)`, `Currency`
  * `ClientReferenceNumber`, `FullName`
  * Flags : `IsPrelevementDone bit`, `IsNSFDone bit`, `IsException bit`, `IsVerifier bit`

## Impression

* **Logo** : `Resources/logo.png`
* **PDF/Print** : GDI+ (System.Drawing.Printing) — pas d’API C# 8+.
* Conformité au document `docs/PrintLayouts.md` (source de vérité).

## Build & Solution

* Le `.csproj` doit **inclure** tous les `.cs` (pas de fichier orphelin).
* **Namespace par défaut** cohérent (`TransactionViewer`), sous-namespaces selon l’arborescence.

## Git & Documentation

* `README.md` racine (objectif, build, config rapide)
* Dossier `docs/` avec : `PrintLayouts.md`, `Compatibility.md`, `Architecture.md`, `Changelog.md`

---

Cette page définit les *contraintes immuables* utilisées pour tout développement et revue de code.
