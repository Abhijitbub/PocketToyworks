# Game UI verification requirement

The user explicitly requires UI verification before delivering game builds.

- Inspect the user's latest screenshots/recordings and reproduce the reported state.
- Review actual rendered gameplay, tutorial, home, profile, settings, queues,
  result and economy screens before handing over an APK.
- Include short/tall portrait layouts, the reported phone resolution and
  explicit portrait 16:9 (9:16) and 18:9 (9:18) screens, plus
  simulated safe areas. Verify the captured framebuffer size; a requested
  resolution alone does not prove that resolution was rendered.
- Check texture dimensions/compression, proportional scaling, sliced corners,
  background coverage, readable scalable text, text overflow and preview bounds.
  Verify headings within the actual colored panel fill, including empty states;
  outer RectTransform containment alone does not catch decorative-rim overlap.
- Keep the automated font/import/geometry regressions passing. Fix clipping or
  unreadable text instead of suppressing the checks or shrinking it indiscriminately.
- Inspect full-screen captures and native-resolution crops. A geometry test alone
  does not establish visual quality. Separate desktop checks from physical Android
  touch, keyboard, safe-area and performance validation in the handover.
- Preserve existing save compatibility and verify rotation/drag/tutorial flows.
- Do not claim perfection or physical-device testing without supporting evidence.
