App Version (打包版本号/Bundle Version) 和 Build Number (构建版本号/Version Code) 在Unity 和各大应用商店中有着截然不同的定义和用途。
  1. Bundle Version (App Version)
   * Unity 设置: PlayerSettings.bundleVersion
   * 示例: 1.0, 1.2.5, 2.0
   * 用途: 给用户看的。
       * 在 App Store / Google Play 的详情页上显示的“版本”。
       * 游戏内 Application.version 获取到的值。
       * 通常用于标识功能迭代或重大更新。
   * 热更意义: 在你的架构中，这个版本号决定了客户端去下载哪个 filelist_{version}.json。
       * 如果客户端是 1.0，它就认准了要下载 filelist_1.0.json。

  2. Build Number (Version Code)
   * Unity 设置:
       * Android: PlayerSettings.Android.bundleVersionCode (整数，如 1, 2, 100)
       * iOS: PlayerSettings.iOS.buildNumber (字符串，如 "1", "1.0.1", "20230101")
   * 示例: 10, 102
   * 用途: 给应用商店看的。
       * 它是应用商店判断“这是不是一个新包”的唯一标准。
       * 即使你的 Bundle Version 没变（还是 1.0），只要 Build Number 变大（从 10 变成
         11），商店就认为这是一个新的上传，允许你提交审核或覆盖。
       * 不可回退：商店通常要求这个数字只增不减。
   * 热更意义: 通常与热更逻辑无关。你的热更脚本不关心这个数字。

  3. 它们的关系与实战策略

  场景 A：纯热更 (Resource Update)
   * Bundle Version: 1.0 (不变)
   * Build Number: 10 (不变，因为没重新打 APK)
   * 操作：你打了一个 Addressables Update，上传到 CDN。
   * 结果：用户不需要去商店，直接在游戏内下载更新。

  场景 B：重新打整包 (Re-build APK) 但功能不变
   * Bundle Version: 1.0 (不变，因为只是修复了包内的一个闪退 Bug，或者想换个签名)
   * Build Number: 必须加 1 (变成 11)。
   * 操作：上传新 APK 到商店。
   * 结果：
       * 商店接受上传（因为 Build Number 变了）。
       * 用户看到的版本还是 1.0。
       * 重点：因为 Bundle Version 还是 1.0，新 APK 依然会去请求 filelist_1.0.json。这保证了新老包在资源上的兼容性。

  场景 C：发布新版本 (Feature Release)
   * Bundle Version: 改为 1.1。
   * Build Number: 必须加 1 (变成 12)。
   * 操作：上传新 APK。生成 filelist_1.1.json。
   * 结果：
       * 商店显示“更新到 1.1”。
       * 1.1 的用户下载 filelist_1.1.json。
       * 1.0 的用户（如果没去商店更）依然用 filelist_1.0.json。完美隔离。