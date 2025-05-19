# 动态生成实际使用的css样式
```csharp
// html
<p class="mg-20"></p>
<div class="layout-main"></p>
// 生成输出css文件包含
.mg-20 {}
.layout-main{}
```
## 生成规则
1. 从已有的css文件查找出规则,根据类名".class"
```csharp
  // 源css文件
  .txt-red{color:red}
  .txt-blue{color:blue}
  // html
  <p class="txt-red"></p>
  // 匹配到txt-red,选取输出
  .txt-red
  {
    color:red
  }
```
2. 根据类名动态生成规则
```csharp
  <p class="mg-20"></p>
  // 根据简化样式名 mg-20 生成输出
  .mg-20 {
    margin:20px;
  }
```
## 使用
主要文件UsedCss.cs
```csharp
new UsedCss().Run("index.html", "output.css");
```
## 处理过程
1. 载入规则集和数据
2. 取出html中所有使用到的class
3. 遍历class,到规则集匹配,抽取css样式
4. 简化合并相同规则集
5. 输出css文件

## 默认数据
##### 在dataDefault目录下
1. globalBase.css 含有全局css
2. complexRule.css 预定义样式
3. simpleRules.ini 常用css简单规则,一般只包含一个规则的原子风格.
4. styleSimpleName.ini 类简化名字和样式规则对应关系
5. styleValues.ini 预定义样式值

##### ini文件格式

1. 用等号分割键和值 "k=v" ,可用 "// 注释",键/值前后不要空格.每个键值对或者注释占一行.
2. simpleRules的键是简化类名字,值是css规则例如:
```ini
  txt-center=text-align:center
```
3. styleSimpleName和simpleRules相似,键也是简化名字,值也是css规则,但规则的值是占位符"$v",具体值动态生成后替换.例如:
```ini
  // $v值示例: 20px / 20vw / 20rem / 20%
  mg=margin:$v
  bg-black,bg-white=background-color:$v
```
4. styleValues的键是类简化名字的后部分,值是css规则值.例如:
```ini
  // black0表示颜色值是 #000
  // black0对应简化类名 "bg-black-0" 的后2段
  black0=#000
```

## 特点
##### 简化类名组成规则