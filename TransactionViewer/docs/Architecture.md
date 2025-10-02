# Architecture — TransactionViewer

Ce document décrit la structure modulaire du projet et les dépendances principales.

## 1) Arborescence & Namespaces

```
TransactionViewer.sln
TransactionViewer/
  Program.cs
  Form1.cs
  Models/
    Transaction.cs
  DataAccess/
    TransactionRepository.cs
  Helpers/
    ClientRefHelper.cs
  Services/
    JsonImportService.cs
    CsvExporter.cs
    ArchiveService.cs
  Printing/
    IPrintManager.cs
    PrintManager.cs
    PrintManagerFailed.cs
    PrintManagerException.cs
  Resources/
    logo.png
  docs/
    PrintLayouts.md
    Compatibility.md
    Architecture.md
    Changelog.md
```

## 2) Diagramme (texte) des dépendances

```
Program --> Form1                : Application.Run(Form1)
Form1 ..> Transaction            : lit / met à jour états
Form1 ..> TransactionRepository  : persistance (Get*, Update*, InsertOrUpdate)
Form1 ..> JsonImportService      : importation JSON
Form1 ..> ClientRefHelper        : format/fallback # Client
Form1 ..> CsvExporter            : export CSV (NSF)
Form1 ..> ArchiveService         : archive le CSV
Form1 ..> PrintManager           : impression Prélèvements (portrait)
Form1 ..> PrintManagerFailed     : impression NSF (paysage)
Form1 ..> PrintManagerException  : impression Exceptions (paysage)

TransactionRepository ..> Transaction : CRUD
CsvExporter ..> Transaction           : sérialisation
ArchiveService .. (FS)                : déplacement fichiers
PrintManager* ..> Transaction         : rendu impression
ClientRefHelper ..> Transaction       : fallback client
```

## 3) Règles de couche

* **UI (Form1)** : aucune logique d’accès SQL direct ; utilise `TransactionRepository`.
* **Services** : traitement procédural transverse (import JSON, export CSV, archivage).
* **DAL** : `TransactionRepository` encapsule le SQL, conversions (TryParse), et les flags.
* **Printing** : aucune mutation de données, lecture-only + formatage conforme à `docs/PrintLayouts.md`.

## 4) Points d’extension

* Ajout d’un nouveau type de transaction :

  * Nouvelle table/flag si nécessaire.
  * Nouveau `PrintManagerX` si un rendu spécifique est requis.
  * Ajout du mapping dans `JsonImportService` et/ou `TransactionRepository`.

## 5) Contrats immuables

* Respect **strict** de `docs/PrintLayouts.md` pour l’ordre/entêtes/format.
* Compatibilité **C# 7.3** (voir `docs/Compatibility.md`).
* Namespaces **stables**.

---

*Architecture validée pour TransactionViewer 0.0.22+.*
