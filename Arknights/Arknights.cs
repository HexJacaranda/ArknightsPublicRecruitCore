using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HexJson;

namespace Arknights
{
    public static class EnumerableExtension
    {
        public static T MinOf<T>(this IEnumerable<T> Target, Func<T, T, int> Comparator)
        {
            T key = Target.FirstOrDefault();
            if (key == default)
                return default;
            foreach (var item in Target)
                if (Comparator(key, item) > 0)
                    key = item;
            return key;
        }
        public static T MaxOf<T>(this IEnumerable<T> Target, Func<T, T, int> Comparator)
        {
            T key = Target.FirstOrDefault();
            if (key == default)
                return default;
            foreach (var item in Target)
                if (Comparator(key, item) < 0)
                    key = item;
            return key;
        }
    }

    [JsonObjectification]
    public class Operator
    {
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "code_name")]
        public string CodeName { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "belong")]
        public string Belong { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "profession")]
        public string Profession { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "rank")]
        public int Rank { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "sex")]
        public string Sex { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "infect")]
        public bool Infect { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "way")]
        public string Way { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "health")]
        public int Health { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "attack")]
        public int Attack { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "protect")]
        public int Protect { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "magic_protect")]
        public int MagicProtect { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "deploy_again")]
        public string DeployAgain { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "deploy")]
        public int Deploy { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "perfect_deploy")]
        public int PerfectDeploy { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "block")]
        public int Block { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "attack_speed")]
        public string AttackSpeed { get; set; }
        [JsonField(Flag = FieldFlag.PrimitiveType, Target = "attribute")]
        public string Attribute { get; set; }
        [JsonField(Flag = FieldFlag.List, Target = "tags")]
        public IList<string> Tags { get; set; }
        public static Func<Operator, Operator, int> Comparator => (left, right) => left.Rank - right.Rank;
    }
   
    public class Recruit
    {
        private Operator[] m_all_operators;
        private string[] m_all_tags;
        private Dictionary<string, HashSet<Operator>> m_classes;
        private Dictionary<string, string[]> m_tag_categories;
        public Recruit(string OperatorsJson, string CategoriesJson, Func<Operator, bool> OperatorFilter)
        {
            JsonParser operator_parser = new JsonParser(OperatorsJson);
            JsonArray operator_array = operator_parser.ParseArray();
            JsonObjectification<Operator> objectify = new JsonObjectification<Operator>();

            List<Operator> operators = new List<Operator>();
            m_classes = new Dictionary<string, HashSet<Operator>>();
            HashSet<string> tags_set = new HashSet<string>();

            for (int i = 0; i < operator_array.Count; ++i)
            {
                Operator single = objectify.Go(operator_array.GetObject(i));
                if (!OperatorFilter(single))
                    continue;
                foreach (var tag in single.Tags)
                {
                    if (!m_classes.ContainsKey(tag))
                        m_classes.Add(tag, new HashSet<Operator>());
                    tags_set.Add(tag);
                    m_classes[tag].Add(single);
                }
                operators.Add(single);
            }
            m_all_tags = tags_set.ToArray();
            m_all_operators = operators.ToArray();

            m_tag_categories = new Dictionary<string, string[]>();
            JsonParser tag_categories_parser = new JsonParser(CategoriesJson);
            JsonArray tag_categories_array = tag_categories_parser.ParseArray();
            for (int i = 0; i < tag_categories_array.Count; ++i)
            {
                var category = tag_categories_array.GetObject(i);
                m_tag_categories.Add(category.GetValue("Cat").AsString(),
                    (from tag in category.GetArray("Tags") select (tag as JsonValue).AsString()).ToArray());
            }
        }
        public Operator[] Operators => m_all_operators;
        public string[] OperatorTags => m_all_tags;
        public Dictionary<string, HashSet<Operator>> Classes => m_classes;
        public Dictionary<string, string[]> TagCategories => m_tag_categories;
        private static T[] Subset<T>(T[] Target, int Result)
        {
            List<T> ret = new List<T>();
            for (int i = 0; i < Target.Length; ++i)
                if ((Result & (1 << i)) != 0) ret.Add(Target[i]);
            return ret.ToArray();
        }
        private static IEnumerable<T[]> PartiallyCombine<T>(T[] Target)
        {
            for (int i = 0; i < (1 << Target.Length); ++i)
                yield return Subset(Target, i);         
        }
        public IEnumerable<KeyValuePair<string[], Operator[]>> AllOf(string[] Tags, Func<Operator, bool> Filter)
        {
            foreach (var combination in PartiallyCombine(Tags))
            {
                if (combination.Length == 0)
                    continue;
                HashSet<Operator> set = new HashSet<Operator>(m_all_operators);
                foreach (var tag in combination)
                {
                    set.IntersectWith(Classes[tag]);
                    if (set.Count == 0)
                        break;
                }
                if (set.Count != 0)
                    yield return new KeyValuePair<string[], Operator[]>(combination, set.Where(Filter).ToArray());
            }
        }
        public IEnumerable<KeyValuePair<string[], Operator[]>> BestOf(string[] Tags, Func<Operator, bool> Filter)
        {
            var pairs = AllOf(Tags, Filter);
            var mins = from pair in pairs select pair.Value.MinOf(Operator.Comparator);
            var local_max = mins.MaxOf(Operator.Comparator);
            if (local_max == null)
                yield break;
            foreach (var pair in pairs)
            {
                var min = pair.Value.MinOf(Operator.Comparator);
                if (local_max.Rank > min.Rank)
                    continue;
                var candidate = pair.Value.Where(target => target.Rank == local_max.Rank).ToArray();
                if (candidate.Length == 0)
                    continue;
                if (candidate.FirstOrDefault()?.Rank == 6 && !pair.Key.Contains("高级资深干员"))
                    continue;
                yield return new KeyValuePair<string[], Operator[]>(pair.Key, candidate);
            }
        }
    }
}
