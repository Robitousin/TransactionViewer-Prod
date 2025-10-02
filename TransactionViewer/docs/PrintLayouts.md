# Spécifications d'impression — TransactionViewer

Ce document **verrouille** le contrat d'affichage des 3 impressions. Toute évolution doit **mettre à jour ce fichier d'abord**.

> Conventions globales
>
> * **Culture** : fr-CA (virgule décimale)
> * **Montant** : `N2` + espace + `$` → ex. `144,00 $`
> * **Date** : `dd/MM/yyyy` (affichage)
> * **Police** (suggestion) : Arial 9 (données), Arial 10 gras (entêtes), Arial 14–15 gras (titres)

---

## 1) Prélèvements (portrait)

**Titres**

* Ligne 1 : `Rapport de Transactions`
* Ligne 2 : `Prélèvements`
* Cartouche 1re page :

  * `Nom: {NomEntreprise}` (fixe: `8341855 Canada Inc`)
  * `Type: {TransactionType}` (ex.: `EFT Funding`)
  * `Total: {Σ CreditAmount} $`
  * `Référence : {date de la 1re Transaction.TransactionDateTime}` (format `dd/MM/yyyy`)

**Colonnes (ordre + en-têtes + mapping)**

| # | En-tête       | Mapping modèle                           | Align  |
| - | ------------- | ---------------------------------------- | ------ |
| 1 | # Client      | `Transaction.ClientReferenceNumber`      | Left   |
| 2 | Nom du client | `Transaction.FullName`                   | Left   |
| 3 | Montant       | `Transaction.CreditAmount` → format €$   | Right  |
| 4 | Transmis Le   | `Transaction.TransactionDateTime` → date | Center |
| 5 | TransactionID | `Transaction.TransactionID`              | Center |

**Remarques**

* Regroupement à l'impression par **date** de `TransactionDateTime` (date seule).
* `Total` = somme des `CreditAmount` affichés dans le regroupement documenté.

---

## 2) NSF (paysage)

**Titres**

* Ligne 1 : `Rapport de Transactions`
* Ligne 2 : `NSF`
* Cartouche 1re page :

  * `Nom : {NomEntreprise}`
  * `Type: {TransactionType}`
  * `Total : {Σ CreditAmount} $`
  * `Référence : {date de la 1re Transaction.LastModified}` (format `dd/MM/yyyy`)

**Colonnes (ordre + en-têtes + mapping)**

| # | En-tête       | Mapping modèle                           | Align  |
| - | ------------- | ---------------------------------------- | ------ |
| 1 | # Client      | `Transaction.ClientReferenceNumber`      | Left   |
| 2 | Nom du Client | `Transaction.FullName`                   | Left   |
| 3 | Montant       | `Transaction.CreditAmount` → format €$   | Right  |
| 4 | Date NSF      | `Transaction.LastModified` → date        | Center |
| 5 | Transmis Le   | `Transaction.TransactionDateTime` → date | Center |
| 6 | Code          | `Transaction.TransactionErrorCode`       | Left   |
| 7 | NSF Raison    | `Transaction.TransactionFailureReason`   | Left   |

**Remarques**

* Tri **ascendant** par Montant avant pagination.
* Regroupement à l'impression par **date** de `LastModified` (date seule).

---

## 3) Exceptions (paysage) — *à figer plus tard*

**Titres**

* Ligne 1 : `Rapport de Transactions`
* Ligne 2 : `Exceptions`

**Colonnes (proposition actuelle — à valider)**

| # | En-tête        | Mapping modèle                           | Align  |
| - | -------------- | ---------------------------------------- | ------ |
| 1 | # Client       | `Transaction.ClientReferenceNumber`      | Left   |
| 2 | Nom du Client  | `Transaction.FullName`                   | Left   |
| 3 | Montant        | `Transaction.CreditAmount` → format €$   | Right  |
| 4 | Transmis Le    | `Transaction.TransactionDateTime` → date | Center |
| 5 | Date Exception | `Transaction.LastModified` → date        | Center |
| 6 | Code           | `Transaction.TransactionErrorCode`       | Left   |
| 7 | Raison         | `Transaction.TransactionFailureReason`   | Left   |

---

## 4) Règles d’affichage communes

* **Logo** : `Resources/logo.png` (120×86 px environ) en 1re page, si présent.
* **Pied de page** :

  * Gauche : `Date : {Date du jour}` (format `dd/MM/yyyy`)
  * Droite : `Page {n}`
* **Fallback #Client** : si `ClientReferenceNumber` est vide, évaluer un *helper* (`ClientRefHelper`) ; ne jamais écrire `null`.
* **N/A** : préférer vide `""` plutôt que `N/A` pour respecter les maquettes.

---

## 5) Contrôles avant impression (auto-checks)

* Vérifier que la **liste d’en-têtes** générée == à la table ci-dessus.
  → si différent : *log + MessageBox* “Incohérence layout – MAJ docs requise”.
* Valider formats :

  * Date → `TryParse` + `ToString("dd/MM/yyyy")`
  * Montant → `decimal.TryParse(fr-CA)` + `N2 + " $"`

---

## 6) Mapping résumé (clé → propriété)

* `Client` → `Transaction.ClientReferenceNumber`
* `Nom` → `Transaction.FullName`
* `Montant` → `Transaction.CreditAmount`
* `Transmis Le` → `Transaction.TransactionDateTime`
* `Date NSF / Date Exception` → `Transaction.LastModified`
* `Code` → `Transaction.TransactionErrorCode`
* `Raison` → `Transaction.TransactionFailureReason`

---

*Ce document est la **source de vérité** pour toute modification d’impression.*
