namespace SafarSense;

/// <summary>
/// The "documents" our RAG app can search. In a real app these would come from
/// files, a database or a CMS and be split into chunks; here each sentence is one chunk.
/// </summary>
public static class KnowledgeBase
{
    public static readonly string[] Destinations =
    [
        "Goa is famous for its beaches, Portuguese-era churches and seafood, and the best time to visit is November to February.",
        "Kerala's backwaters around Alleppey can be explored on traditional houseboats called kettuvallams, a favourite for honeymooners.",
        "Jaipur, the Pink City of Rajasthan, is known for Amber Fort, Hawa Mahal and colourful bazaars selling block-print textiles.",
        "Ladakh has high-altitude lakes like Pangong Tso, and travellers should rest a day or two after arriving to avoid altitude sickness.",
        "Varanasi on the banks of the Ganga is one of the oldest living cities, famous for the evening Ganga Aarti at Dashashwamedh Ghat.",
        "Darjeeling in West Bengal is known for its tea gardens, views of Kanchenjunga and the UNESCO-listed toy train.",
        "Hampi in Karnataka has the ruins of the Vijayanagara Empire's temples and bazaars, scattered among giant boulders.",
        "The Rann of Kutch in Gujarat becomes a vast white salt desert, and the Rann Utsav festival is held there in winter.",
        "Meghalaya is home to living root bridges grown by the Khasi people, and Mawsynram is one of the wettest places on Earth.",
        "The Andaman Islands are popular for scuba diving and snorkelling, and Radhanagar Beach is known for its turquoise water.",
    ];
}
