import importlib.util
import pathlib
import sys
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'backend'))
from google_worker import GoogleTranslator, TranslationError
from offline_worker import OfflineTranslator
from glossary import validated_entries, replacement_spans


def entry(source, replacement, target='*'):
    return dict(source=source, replacement=replacement, target=target)


class GlossaryTests(unittest.TestCase):
    def test_online_preserves_sentence_context_and_reorders_terms(self):
        calls = []
        def google(text, direction):
            calls.append(text)
            return '用 ZXQ1 查看 ZXQ0。'
        engine = GoogleTranslator(google, 0)
        self.assertEqual(engine.translate('Check Skills with Claude.', 'auto:zh-CN',
                         [entry('Skills', '技能'), entry('Claude', 'Claude')]), '用 Claude 查看 技能。')
        self.assertEqual(calls, ['Check ZXQ0 with ZXQ1.'])

    def test_lost_google_placeholder_falls_back_without_losing_terms(self):
        engine = GoogleTranslator(lambda text, direction: 'lost' if 'ZXQ' in text else '[' + text + ']', 0)
        self.assertEqual(engine.translate('Open Skills now.', 'auto:zh-CN', [entry('Skills', '技能')]), '[Open] 技能 [now.]')
        self.assertEqual(engine.fallbacks, 1)

    def test_protected_only_term_replaced_without_contacting_google(self):
        calls = []
        engine = GoogleTranslator(lambda text, direction: calls.append(text) or text, 0)
        self.assertEqual(engine.translate('Skills', 'auto:zh-CN', [entry('Skills', '技能')]), '技能')
        self.assertEqual(calls, [])

    def test_code_links_fences_and_indented_code_remain_exact(self):
        source = 'Claude `Claude` https://example.org/Claude\n\n```\nClaude\n```\n    Claude\n'
        expected = '鲸鱼 `Claude` https://example.org/Claude\n\n```\nClaude\n```\n    Claude\n'
        for engine in (GoogleTranslator(lambda text, direction: text, 0), OfflineTranslator(lambda text, direction: text)):
            self.assertEqual(engine.translate(source, 'en_zh', [entry('Claude', '鲸鱼')]), expected)

    def test_longest_literal_case_and_identifier_boundary(self):
        engine = GoogleTranslator(lambda text, direction: text, 0)
        glossary = [entry('Claude', 'C'), entry('Claude Code', 'CC'), entry('A+B', 'sum')]
        self.assertEqual(engine.translate('Claude Code Claude ClaudeX claude A+B', 'auto:en', glossary), 'CC C ClaudeX claude sum')

    def test_target_specific_overrides_all_and_ignores_other_languages(self):
        glossary = [entry('Skills', 'Skills'), entry('Skills', '技能', 'zh-CN'), entry('Skills', 'スキル', 'ja')]
        for engine in (GoogleTranslator(lambda t,d:t, 0), OfflineTranslator(lambda t,d:t)):
            self.assertEqual(engine.translate('Skills', 'en_zh', glossary), '技能')
            self.assertEqual(engine.translate('Skills', 'zh_en', glossary), 'Skills')
        self.assertEqual(GoogleTranslator(lambda t,d:t, 0).translate('Skills','auto:ja',glossary), 'スキル')

    def test_cache_does_not_keep_old_replacement(self):
        engine = GoogleTranslator(lambda t,d:t, 0)
        self.assertEqual(engine.translate('Read Skills.', 'en_zh', [entry('Skills','技能')]), 'Read 技能.')
        self.assertEqual(engine.translate('Read Skills.', 'en_zh', [entry('Skills','能力')]), 'Read 能力.')

    def test_offline_terms_never_enter_model_and_keep_newlines(self):
        calls = []
        engine = OfflineTranslator(lambda t,d: calls.append(t) or '[' + t + ']')
        self.assertEqual(engine.translate('Open Claude.\r\n\r\nRead Skills.', 'en_zh', [entry('Claude','Claude'),entry('Skills','技能')]), '[Open] Claude.\r\n\r\n[Read] 技能.')
        self.assertFalse(any('Claude' in text or 'Skills' in text for text in calls))

    def test_chinese_phrase_can_be_adjacent(self):
        engine = OfflineTranslator(lambda t,d:t)
        self.assertEqual(engine.translate('请用大肥鱼继续', 'zh_en', [entry('大肥鱼','Whale')]), '请用Whale继续')

    def test_malformed_and_oversized_dictionaries_rejected(self):
        bad = [ {}, 'wrong', [entry('a','b')]*101, [entry('a','b'),entry('a','c')],
                [entry('a\nb','c')], [entry('a','')], [entry('a'*65,'b')], [entry('a','b','url://x')] ]
        for value in bad:
            with self.assertRaises(ValueError): validated_entries(value)
        with self.assertRaisesRegex(TranslationError, 'invalid_glossary'):
            GoogleTranslator(lambda t,d:t,0).translate('sample','en_zh',{})

    def test_google_errors_not_retried_as_fragments(self):
        calls=[]
        def fail(text,direction):
            calls.append(text)
            raise TranslationError('rate_limited')
        with self.assertRaisesRegex(TranslationError,'rate_limited'):
            GoogleTranslator(fail,0).translate('Read Skills now.','en_zh',[entry('Skills','技能')])
        self.assertEqual(len(calls),1)


if __name__ == '__main__': unittest.main()
